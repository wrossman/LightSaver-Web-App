using Microsoft.EntityFrameworkCore;
public class RemoveStaleLightroomUpdateSessionsService : BackgroundService
{
    private readonly ILogger<RemoveStaleLightroomUpdateSessionsService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    public RemoveStaleLightroomUpdateSessionsService(IServiceScopeFactory scopeFactory, ILogger<RemoveStaleLightroomUpdateSessionsService> logger, IConfiguration config)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _config = config;
    }
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                LightroomUpdateSessionDbContext sessionDb = scope.ServiceProvider.GetRequiredService<LightroomUpdateSessionDbContext>();

                double expire = _config.GetValue<double>("SessionExpiration");

                var cutoff = DateTime.UtcNow.AddSeconds(expire);

                // Find expired sessions
                var expiredSessions = await sessionDb.UpdateSessions
                .Where(s => s.Expired || s.CreatedAt < cutoff)
                .ToListAsync(cancellationToken);

                sessionDb.UpdateSessions.RemoveRange(expiredSessions);
                await sessionDb.SaveChangesAsync(cancellationToken);

                foreach (LightroomUpdateSession item in expiredSessions)
                {
                    if (item.Expired)
                        _logger.LogInformation($"Removed lightroom update session for rokuId {item.RokuId} marked as expired.");
                    else
                        _logger.LogWarning($"Removed lightroom update session rokuId {item.RokuId} due to session timeout.");
                }
            }
            await Task.Delay(10000, cancellationToken);
        }
    }
}