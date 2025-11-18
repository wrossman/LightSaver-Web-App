using Microsoft.EntityFrameworkCore;
public class RemoveStaleRokuSessionsService(
    IServiceProvider serviceProvider, ILogger<RemoveStaleRokuSessionsService> logger) : IHostedService
{
    private readonly ILogger<RemoveStaleRokuSessionsService> _logger = logger;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            var options = new DbContextOptionsBuilder<RokuSessionDbContext>().UseInMemoryDatabase("RokuSessionDb").Options;
            using RokuSessionDbContext sessionDb = new(options);

            await Task.Delay(10000, cancellationToken);

            _logger.LogInformation("Running Roku Session Cleanup");
            var cutoff = DateTime.UtcNow.AddSeconds(-600);

            // Find expired sessions
            var expiredSessions = await sessionDb.Sessions
            .Where(s => s.Expired || s.CreatedAt < cutoff)
            .ToListAsync();

            sessionDb.Sessions.RemoveRange(expiredSessions);
            await sessionDb.SaveChangesAsync();

            foreach (RokuSession item in expiredSessions)
            {
                if (item.Expired)
                    _logger.LogInformation($"Removed Roku Session for IP: {item.SourceAddress} marked as expired.");
                else
                    _logger.LogWarning($"Removed Roku Session for IP: {item.SourceAddress} due to session timeout.");
            }
        }
    }
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}