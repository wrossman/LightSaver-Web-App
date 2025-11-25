using Microsoft.EntityFrameworkCore;
public class TransferFilesService : BackgroundService
{
    private readonly ILogger<TransferFilesService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    public TransferFilesService(IServiceScopeFactory scopeFactory, ILogger<TransferFilesService> logger)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (UserSessions.CodesReadyForTransfer.TryDequeue(out var sessionCode))
            {
                using (IServiceScope scope = _scopeFactory.CreateScope())
                {
                    UserSessionDbContext userSessionDb = scope.ServiceProvider.GetRequiredService<UserSessionDbContext>();
                    RokuSessionDbContext rokuSessionDb = scope.ServiceProvider.GetRequiredService<RokuSessionDbContext>();

                    var userSession = await userSessionDb.Sessions
                    .FirstOrDefaultAsync(s => s.SessionCode == sessionCode, cancellationToken);
                    if (userSession != null)
                    {
                        userSession.ReadyForTransfer = true;
                        await userSessionDb.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning($"TestSessionCode Failed getting userSession for session code {sessionCode}");
                    }

                    var rokuSession = await rokuSessionDb.Sessions
                    .FirstOrDefaultAsync(s => s.SessionCode == sessionCode, cancellationToken);
                    if (rokuSession != null)
                    {
                        rokuSession.ReadyForTransfer = true;
                        await rokuSessionDb.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning($"TestSessionCode Failed getting rokuSession for session code {sessionCode}");
                    }
                }
            }
            await Task.Delay(1000, cancellationToken);
        }
    }
}