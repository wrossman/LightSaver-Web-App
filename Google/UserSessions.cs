using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Reflection;
using System.Collections.Concurrent;


public class UserSessions
{

    private readonly ILogger<UserSessions> _logger;
    private readonly RokuSessionDbContext _rokuSessionDb;
    private readonly UserSessionDbContext _userSessionDb;
    public UserSessions(ILogger<UserSessions> logger, UserSessionDbContext userSessionDb, RokuSessionDbContext rokuSessionDb)
    {
        _logger = logger;
        _userSessionDb = userSessionDb;
        _rokuSessionDb = rokuSessionDb;
    }
    public static ConcurrentQueue<string> CodesReadyForTransfer { get; set; } = new();
    private static HashSet<string> UserSessionIds { get; set; } = new();
    // private async Task<bool> CheckIpSessionCount(IPAddress ipAddress)
    // {
    //     string ipAddressStr = ipAddress.ToString();
    //     _logger.LogInformation($"User {ipAddressStr} just tried to connect");

    //     int result = await _userSessionDb.Sessions.CountAsync(s => s.SourceAddress == ipAddressStr);

    //     if (result <= 3)
    //         return true;
    //     else
    //         return false;
    // }

    public async Task<string> CreateUserSession(IPAddress ipAddress, string accessToken)
    {
        // if (!await CheckIpSessionCount(ipAddress))
        //     throw new ArgumentException("Too many sessions with current IP");

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
        _userSessionDb.Add(session);
        // add check to ensure that the data was written
        await _userSessionDb.SaveChangesAsync();

        string sessionLog = "The following session was written to the database:";
        foreach (PropertyInfo prop in session.GetType().GetProperties())
        {
            var name = prop.Name;
            var value = prop.GetValue(session, null);
            sessionLog += $"\n{name} = {value}";
        }
        _logger.LogInformation(sessionLog);
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

    public async Task<bool> AssociateToRoku(string sessionCode, string sessionId)
    {
        // THIS METHOD IS A LITTLE OVER KILL I SHOULD PROBABLY FIX IT
        var rokuSession = await _rokuSessionDb.Sessions
        .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        var userSession = await _userSessionDb.Sessions.FindAsync(sessionId);

        if (rokuSession is null)
        {
            _logger.LogWarning($"Roku session could not be found for sessionCode {sessionCode}");
            return false;
        }

        if (userSession != null)
        {
            userSession.SessionCode = sessionCode;
            userSession.RokuId = rokuSession.RokuId;
            await _userSessionDb.SaveChangesAsync();
            _logger.LogInformation("Successfully updated UserSession SessionCode");
        }
        else
            throw new Exception("Unable to update UserSession SessionCode");

        if (rokuSession != null && rokuSession.SessionCode == userSession.SessionCode)
        {
            _logger.LogInformation("RokuSession and UserSession have been associated");
        }
        else
            throw new Exception("RokuSession is unable to be found");

        return true;
    }
}