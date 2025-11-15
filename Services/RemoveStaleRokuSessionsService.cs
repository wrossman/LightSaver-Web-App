using Microsoft.EntityFrameworkCore;
public class RemoveStaleRokuSessionsService(
    IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // using IServiceScope scope = serviceProvider.CreateScope();
        // var options = new DbContextOptionsBuilder<RokuSessionDbContext>().UseInMemoryDatabase("RokuSessionDb").Options;
        // using RokuSessionDbContext sessionDb = new(options);

        while (true)
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            var options = new DbContextOptionsBuilder<RokuSessionDbContext>().UseInMemoryDatabase("RokuSessionDb").Options;
            using RokuSessionDbContext sessionDb = new(options);

            await Task.Delay(10000, cancellationToken);
            // System.Console.WriteLine("Running Roku Session Cleanup");

            var cutoff = DateTime.UtcNow.AddSeconds(-60000);

            // Find expired sessions
            var expiredSessions = await sessionDb.Sessions
                .Where(s => s.CreatedAt < cutoff)
                .ToListAsync();

            sessionDb.Sessions.RemoveRange(expiredSessions);
            await sessionDb.SaveChangesAsync();

            foreach (RokuSession item in expiredSessions)
            {
                System.Console.WriteLine($"Removed Roku Session for IP: {item.SourceAddress}");
            }
        }
    }
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}