using Microsoft.EntityFrameworkCore;
public class RemoveStaleSessionsService(
    IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        var options = new DbContextOptionsBuilder<SessionDbContext>().UseInMemoryDatabase("SessionDb").Options;
        using SessionDbContext sessionDb = new(options);

        while (true)
        {
            await Task.Delay(10000, cancellationToken);
            System.Console.WriteLine("Running Session Cleanup");
            // Define cutoff time (20 seconds ago)
            var cutoff = DateTime.UtcNow.AddSeconds(-10);

            // Find expired sessions
            var expiredSessions = await sessionDb.Sessions
                .Where(s => s.CreatedAt < cutoff)
                .ToListAsync();

            // Remove them
            sessionDb.Sessions.RemoveRange(expiredSessions);
            // remove session key too
            foreach (UserSession session in expiredSessions)
            {
                if (UserSessions.SessionCodes.Remove(session.SessionCode))
                    System.Console.WriteLine($"Successfully removed session code for {session.SourceAddress}");
                else
                    System.Console.WriteLine($"Failed to remove session code for {session.SourceAddress}");
            }
            ;
            // Commit changes
            await sessionDb.SaveChangesAsync();
            foreach (UserSession item in expiredSessions)
            {
                System.Console.WriteLine($"Removed Session for IP: {item.SourceAddress}");
            }
        }
    }
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}