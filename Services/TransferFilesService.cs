using Microsoft.EntityFrameworkCore;
public class TransferFilesService(
    IServiceProvider serviceProvider, ILogger<TransferFilesService> logger) : IHostedService
{
    private readonly ILogger<TransferFilesService> _logger = logger;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            var userOptions = new DbContextOptionsBuilder<UserSessionDbContext>().UseInMemoryDatabase("UserSessionDb").Options;
            var rokuOptions = new DbContextOptionsBuilder<RokuSessionDbContext>().UseInMemoryDatabase("RokuSessionDb").Options;
            using UserSessionDbContext userSessionDb = new(userOptions);
            using RokuSessionDbContext rokuSessionDb = new(rokuOptions);

            if (UserSessions.CodesReadyForTransfer.TryDequeue(out var sessionCode))
            {
                await TestSessionCode(userSessionDb, rokuSessionDb, sessionCode);
            }
            await Task.Delay(1000, cancellationToken);
        }
    }
    public async Task TestSessionCode(UserSessionDbContext userSessionDb, RokuSessionDbContext rokuSessionDb, string sessionCode)
    {
        var userSession = await userSessionDb.Sessions
        .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        if (userSession != null)
        {
            userSession.ReadyForTransfer = true;
            await userSessionDb.SaveChangesAsync();
        }
        else
        {
            _logger.LogWarning($"TestSessionCode Failed getting userSession for session code {sessionCode}");
            return;
        }
        var rokuSession = await rokuSessionDb.Sessions
        .FirstOrDefaultAsync(s => s.SessionCode == userSession.SessionCode);
        if (rokuSession != null)
        {
            rokuSession.ReadyForTransfer = true;
            await rokuSessionDb.SaveChangesAsync();
        }
        else
        {
            _logger.LogWarning($"TestSessionCode Failed getting rokuSession for session code {sessionCode}");
            return;
        }

    }
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}