using Microsoft.EntityFrameworkCore;
public class RemoveStaleUserSessionsService(
    IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        var options = new DbContextOptionsBuilder<UserSessionDbContext>().UseInMemoryDatabase("UserSessionDb").Options;
        using UserSessionDbContext sessionDb = new(options);

        while (true)
        {
            await Task.Delay(10000, cancellationToken);
            // System.Console.WriteLine("Running User Session Cleanup");
            // Define cutoff time (20 seconds ago)
            var cutoff = DateTime.UtcNow.AddSeconds(-60000);

            // Find expired sessions
            var expiredSessions = await sessionDb.Sessions
                .Where(s => s.CreatedAt < cutoff)
                .ToListAsync();
            // Remove them
            sessionDb.Sessions.RemoveRange(expiredSessions);
            // Commit changes
            await sessionDb.SaveChangesAsync();
            foreach (UserSession item in expiredSessions)
            {
                System.Console.WriteLine($"Removed User Session for user {item.SourceAddress}");
            }
        }
    }
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}