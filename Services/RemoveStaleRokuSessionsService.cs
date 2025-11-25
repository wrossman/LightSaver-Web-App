using Microsoft.EntityFrameworkCore;
public class RemoveStaleRokuSessionsService : BackgroundService
{
    private readonly ILogger<RemoveStaleRokuSessionsService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    public RemoveStaleRokuSessionsService(IServiceScopeFactory scopeFactory, ILogger<RemoveStaleRokuSessionsService> logger)
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
                RokuSessionDbContext sessionDb = scope.ServiceProvider.GetRequiredService<RokuSessionDbContext>();

                _logger.LogInformation("Running roku Session Cleanup");
                var cutoff = DateTime.UtcNow.AddSeconds(-480);

                // Find expired sessions
                var expiredSessions = await sessionDb.Sessions
                .Where(s => s.Expired || s.CreatedAt < cutoff)
                .ToListAsync(cancellationToken);

                sessionDb.Sessions.RemoveRange(expiredSessions);
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