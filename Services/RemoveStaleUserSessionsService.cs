using Microsoft.EntityFrameworkCore;
public class RemoveStaleUserSessionsService : BackgroundService
{
    private readonly ILogger<RemoveStaleUserSessionsService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    public RemoveStaleUserSessionsService(IServiceScopeFactory scopeFactory, ILogger<RemoveStaleUserSessionsService> logger)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                UserSessionDbContext sessionDb = scope.ServiceProvider.GetRequiredService<UserSessionDbContext>();

                _logger.LogInformation("Running User Session Cleanup");
                var cutoff = DateTime.UtcNow.AddSeconds(-480);

                // Find expired sessions
                var expiredSessions = await sessionDb.Sessions
                .Where(s => s.Expired || s.CreatedAt < cutoff)
                .ToListAsync(cancellationToken);

                sessionDb.Sessions.RemoveRange(expiredSessions);
                await sessionDb.SaveChangesAsync(cancellationToken);

                foreach (UserSession item in expiredSessions)
                {
                    if (item.Expired)
                        _logger.LogInformation($"Removed User Session for user {item.SourceAddress} marked as expired.");
                    else
                        _logger.LogWarning($"Removed User Session for user {item.SourceAddress} due to session timeout.");
                }
            }
            await Task.Delay(10000, cancellationToken);
        }
    }
}