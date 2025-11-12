using System.Security.Cryptography;
using System.Net;
using Microsoft.EntityFrameworkCore;

public class RokuSessions
{
    private static HashSet<string> SessionCodes { get; set; } = new();

    public static async Task<string> CreateRokuSession(IPAddress ipAddress, RokuSessionDbContext sessionDb)
    {
        if (!await CheckIpSessionCount(ipAddress, sessionDb))
            return string.Empty;

        string ipAddressStr = ipAddress.ToString();

        RokuSession session = new()
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

        System.Console.WriteLine("finished saving roku session to rokuSession database");

        return session.SessionCode;
    }
    private static string GenerateSessionCode()
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
    private static async Task<bool> CheckIpSessionCount(IPAddress ipAddress, RokuSessionDbContext sessionDb)
    {
        string ipAddressStr = ipAddress.ToString();
        System.Console.WriteLine($"Roku device {ipAddressStr} just tried to connect");

        int result = await sessionDb.Sessions.CountAsync(s => s.SourceAddress == ipAddressStr);

        if (result <= 3)
            return true;
        else
            return false;

    }

    public static async Task<bool> CheckReadyTransfer(string sessionCode, RokuSessionDbContext rokuSessionDb)
    {
        var rokuSession = await rokuSessionDb.Sessions
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        if (rokuSession != null && rokuSession.ReadyForTransfer == true)
            return true;
        else
            return false;
    }
}