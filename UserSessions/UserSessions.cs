using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;


public class UserSessions
{

    public static HashSet<string> SessionCodes { get; set; } = new();

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

    public static async Task<string> CreateUserSession(IPAddress ipAddress, SessionDbContext sessionDb)
    {
        if (!await CheckIpSessionCount(ipAddress, sessionDb))
            return string.Empty;

        string ipAddressStr = ipAddress.ToString();

        UserSession session = new()
        {
            Id = 0,
            CreatedAt = DateTime.UtcNow,
            SourceAddress = ipAddressStr,
            // ADD SESSION ID GENERATION
            SessionCode = GenerateSessionCode(),
            ReadyForTransfer = false
        };

        // write usersession to database and write sessioncode to hashset
        sessionDb.Add(session);
        // add check to ensure that the data was written
        await sessionDb.SaveChangesAsync();
        SessionCodes.Add(session.SessionCode);

        System.Console.WriteLine("finished saving to database");

        return session.SessionCode;
    }

    public static string GenerateSessionCode()
    {
        using var rng = RandomNumberGenerator.Create();
        string sessionCode = "";
        do
        {
            var bytes = new byte[4];
            rng.GetBytes(bytes);

            int value = BitConverter.ToInt32(bytes, 0) & int.MaxValue; // ensure non-negative
            int code = value % 0x1000000; // restrict to 6 hex digits (0x000000 - 0xFFFFFF), thanks copilot
            sessionCode = code.ToString("X6");

        } while (SessionCodes.Contains(sessionCode));

        return sessionCode;
    }
}