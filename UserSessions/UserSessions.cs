using System.Net;
using Microsoft.EntityFrameworkCore;

public class UserSessions
{

    public static async Task<bool> CheckIpSessionCount(IPAddress ipAddress, SessionDbContext sessionDb)
    {
        string ipAddressStr = ipAddress.ToString();
        System.Console.WriteLine($"{ipAddressStr} just tried to connect");

        int result = await sessionDb.Sessions.CountAsync(s => s.SourceAddress == ipAddressStr);

        if (result <= 3)
            return true;
        else
            return false;

    }

    public static async Task<bool> CreateUserSession(IPAddress ipAddress, SessionDbContext sessionDb)
    {
        if (!await CheckIpSessionCount(ipAddress, sessionDb))
            return false;

        string ipAddressStr = ipAddress.ToString();

        UserSession session = new()
        {
            Id = 0,
            CreatedAt = DateTime.UtcNow,
            SourceAddress = ipAddressStr,
            // ADD SESSION CODE AND ID GENERATION
            SessionCode = 0
        };

        sessionDb.Add(session);
        // add check to ensure that the data was written
        await sessionDb.SaveChangesAsync();

        System.Console.WriteLine("finished saving to database");

        return true;
    }
}