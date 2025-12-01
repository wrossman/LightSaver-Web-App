using Microsoft.EntityFrameworkCore;
public class RemoveStaleRokuSessionsService : BackgroundService
{
    private readonly ILogger<RemoveStaleRokuSessionsService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    public RemoveStaleRokuSessionsService(IServiceScopeFactory scopeFactory, ILogger<RemoveStaleRokuSessionsService> logger, IConfiguration config)
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
                RokuSessionDbContext sessionDb = scope.ServiceProvider.GetRequiredService<RokuSessionDbContext>();

                double expire = _config.GetValue<double>("SessionExpiration");

                var cutoff = DateTime.UtcNow.AddSeconds(expire);

                // Find expired sessions
                var expiredSessions = await sessionDb.RokuSessions
                .Where(s => s.Expired || s.CreatedAt < cutoff)
                .ToListAsync(cancellationToken);

                sessionDb.RokuSessions.RemoveRange(expiredSessions);
                await sessionDb.SaveChangesAsync(cancellationToken);

                foreach (RokuSession item in expiredSessions)
                {
                    if (item.Expired)
                        _logger.LogInformation($"Removed roku Session for roku {item.SourceAddress} marked as expired.");
                    else
                        _logger.LogWarning($"Removed roku Session for roku {item.SourceAddress} due to session timeout.");
                }
            }
            await Task.Delay(10000, cancellationToken);
        }
    }
}