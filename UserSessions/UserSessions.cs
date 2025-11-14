using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Reflection;


public class UserSessions
{
    public static Queue<string> CodesReadyForTransfer { get; set; } = new();
    private static HashSet<string> UserSessionIds { get; set; } = new();

    private static async Task<bool> CheckIpSessionCount(IPAddress ipAddress, UserSessionDbContext sessionDb)
    {
        string ipAddressStr = ipAddress.ToString();
        System.Console.WriteLine($"User {ipAddressStr} just tried to connect");

        int result = await sessionDb.Sessions.CountAsync(s => s.SourceAddress == ipAddressStr);

        if (result <= 3)
            return true;
        else
            return false;
    }

    public static async Task<string> CreateUserSession(IPAddress ipAddress, UserSessionDbContext sessionDb, string accessToken)
    {
        if (!await CheckIpSessionCount(ipAddress, sessionDb))
            throw new ArgumentException("Too many sessions with current IP");

        string ipAddressStr = ipAddress.ToString();

        UserSession session = new()
        {
            Id = GenerateSessionId(),
            CreatedAt = DateTime.UtcNow,
            AccessToken = accessToken,
            SourceAddress = ipAddressStr,
            SessionCode = "",
            ReadyForTransfer = false
        };

        // write usersession to database and write sessioncode to hashset
        sessionDb.Add(session);
        // add check to ensure that the data was written
        await sessionDb.SaveChangesAsync();
        System.Console.WriteLine("The following session was written to the database:");
        foreach (PropertyInfo prop in session.GetType().GetProperties())
        {
            var name = prop.Name;
            var value = prop.GetValue(session, null);
            Console.WriteLine($"{name} = {value}");
        }
        return session.Id;
    }

    private static string GenerateSessionId()
    {
        using var rng = RandomNumberGenerator.Create();
        string userSessionId;
        //thanks copilot
        do
        {
            // Generate 16 random bytes (128 bits)
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            // Convert to hex string (32 hex chars)
            userSessionId = Convert.ToHexString(bytes); // e.g., "A1B2C3D4..."

        } while (UserSessionIds.Contains(userSessionId));

        return userSessionId;
    }

    public static async Task<bool> AssociateToRoku(string sessionCode, string sessionId, UserSessionDbContext userSessionDb, RokuSessionDbContext rokuSessionDb)
    {
        // THIS METHOD IS A LITTLE OVER KILL I SHOULD PROBABLY FIX IT
        var rokuSession = await rokuSessionDb.Sessions
        .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        var userSession = await userSessionDb.Sessions.FindAsync(sessionId);

        if (rokuSession is null)
        {
            System.Console.WriteLine($"Roku session could not be found for sessionCode {sessionCode}");
            return false;
        }

        if (userSession != null)
        {
            userSession.SessionCode = sessionCode;
            userSession.RokuId = rokuSession.RokuId;
            await userSessionDb.SaveChangesAsync();
            System.Console.WriteLine("Successfully updated UserSession SessionCode");
        }
        else
            throw new Exception("Unable to update UserSession SessionCode");

        if (rokuSession != null && rokuSession.SessionCode == userSession.SessionCode)
        {
            System.Console.WriteLine("RokuSession and UserSession have been associated");
        }
        else
            throw new Exception("RokuSession is unable to be found");

        // var test = await rokuSessionDb.Sessions
        // .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        // foreach (PropertyInfo prop in test.GetType().GetProperties())
        // {
        //     var name = prop.Name;
        //     var value = prop.GetValue(test, null);
        //     Console.WriteLine($"{name} = {value}");
        // }

        // var userTest = await userSessionDb.Sessions
        // .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        // foreach (PropertyInfo prop in userTest.GetType().GetProperties())
        // {
        //     var name = prop.Name;
        //     var value = prop.GetValue(userTest, null);
        //     Console.WriteLine($"{name} = {value}");
        // }

        return true;
    }

}