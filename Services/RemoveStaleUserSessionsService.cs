using Microsoft.EntityFrameworkCore;
public class RemoveStaleUserSessionsService(
    IServiceProvider serviceProvider, ILogger<RemoveStaleUserSessionsService> logger) : IHostedService
{
    private readonly ILogger<RemoveStaleUserSessionsService> _logger = logger;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            var options = new DbContextOptionsBuilder<UserSessionDbContext>().UseInMemoryDatabase("UserSessionDb").Options;
            using UserSessionDbContext sessionDb = new(options);

            await Task.Delay(10000, cancellationToken);

            _logger.LogInformation("Running User Session Cleanup");
            var cutoff = DateTime.UtcNow.AddSeconds(-600);

            // Find expired sessions
            var expiredSessions = await sessionDb.Sessions
            .Where(s => s.Expired || s.CreatedAt < cutoff)
            .ToListAsync();

            sessionDb.Sessions.RemoveRange(expiredSessions);
            await sessionDb.SaveChangesAsync();

            foreach (UserSession item in expiredSessions)
            {
                _logger.LogWarning($"Removed User Session for user {item.SourceAddress} due to session timeout.");
            }
        }
    }
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}