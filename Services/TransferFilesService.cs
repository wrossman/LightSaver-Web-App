using Microsoft.EntityFrameworkCore;
using System.Reflection;
public class TransferFilesService(
    IServiceProvider serviceProvider) : IHostedService
{
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
    public static async Task TestSessionCode(UserSessionDbContext userSessionDb, RokuSessionDbContext rokuSessionDb, string sessionCode)
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
            System.Console.WriteLine("TestSessionCode Failed getting userSession");
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
            System.Console.WriteLine("TestSessionCode Failed getting rokuSession");
            return;
        }
        foreach (PropertyInfo prop in userSession.GetType().GetProperties())
        {
            var name = prop.Name;
            var value = prop.GetValue(userSession, null);
            Console.WriteLine($"{name} = {value}");
        }
        foreach (PropertyInfo prop in rokuSession.GetType().GetProperties())
        {
            var name = prop.Name;
            var value = prop.GetValue(rokuSession, null);
            Console.WriteLine($"{name} = {value}");
        }
    }
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}