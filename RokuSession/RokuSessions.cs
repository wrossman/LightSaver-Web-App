using System.Security.Cryptography;
using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text;
using System.Text.Json;
public class RokuSessions
{
    private static HashSet<string> SessionCodes { get; set; } = new();

    public static async Task<string> CreateRokuSession(IPAddress ipAddress, RokuSessionDbContext sessionDb, string rokuId)
    {
        // TODO: also check if we already have a roku session with this device id.
        if (!await CheckIpSessionCount(ipAddress, sessionDb))
            return string.Empty;

        RokuSession session = new()
        {
            Id = 0,
            RokuId = rokuId,
            CreatedAt = DateTime.UtcNow,
            SourceAddress = ipAddress.ToString(),
            SessionCode = GenerateSessionCode(),
            ReadyForTransfer = false
        };

        // write usersession to database and write sessioncode to hashset
        sessionDb.Add(session);
        // add check to ensure that the data was written
        await sessionDb.SaveChangesAsync();

        System.Console.WriteLine("finished saving the following roku session to rokuSession database");
        foreach (PropertyInfo prop in session.GetType().GetProperties())
        {
            var name = prop.Name;
            var value = prop.GetValue(session, null);
            Console.WriteLine($"{name} = {value}");
        }
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
        if (rokuSession is not null)
        {
            // foreach (PropertyInfo prop in rokuSession.GetType().GetProperties())
            // {
            //     var name = prop.Name;
            //     var value = prop.GetValue(rokuSession, null);
            //     Console.WriteLine($"{name} = {value}");
            // }
        }
        if (rokuSession != null && rokuSession.ReadyForTransfer == true)
            return true;
        else
            return false;
    }

    public static async Task<string> ReadRokuPost(HttpContext context)
    {
        context.Request.EnableBuffering(); // allows re-reading the stream

        const int maxBytes = 512;
        var buffer = new byte[32];
        int totalBytes = 0;

        using var memoryStream = new MemoryStream();
        int bytesRead;

        do
        {
            bytesRead = await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
            totalBytes += bytesRead;

            if (totalBytes > maxBytes)
                return "fail";

            await memoryStream.WriteAsync(buffer, 0, bytesRead);
        }
        while (bytesRead > 0);

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
}