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
    public async Task<string> CreateGoogleUserSession(IPAddress ipAddress, string accessToken)
    {
        string ipAddressStr = ipAddress.ToString();

        UserSession session = new()
        {
            Id = await GenerateSessionId(),
            CreatedAt = DateTime.UtcNow,
            AccessToken = accessToken,
            SourceAddress = ipAddressStr,
            SessionCode = "",
            ReadyForTransfer = false,
            Expired = false
        };

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
    public async Task<string> CreateUploadUserSession(IPAddress ipAddress, string sessionCode)
    {
        string ipAddressStr = ipAddress.ToString();

        UserSession session = new()
        {
            Id = await GenerateSessionId(),
            CreatedAt = DateTime.UtcNow,
            AccessToken = "",
            SourceAddress = ipAddressStr,
            SessionCode = sessionCode,
            ReadyForTransfer = false,
            Expired = false
        };

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
    public async Task<UserSession> CreateUserSession(IPAddress ipAddress, string sessionCode)
    {
        string ipAddressStr = ipAddress.ToString();

        UserSession session = new()
        {
            Id = await GenerateSessionId(),
            CreatedAt = DateTime.UtcNow,
            AccessToken = "",
            SourceAddress = ipAddressStr,
            SessionCode = sessionCode,
            ReadyForTransfer = false,
            Expired = false
        };
        _userSessionDb.Add(session);
        // add check to ensure that the data was written
        await _userSessionDb.SaveChangesAsync();

        string sessionLog = "The following user session was written to the database:";
        foreach (PropertyInfo prop in session.GetType().GetProperties())
        {
            var name = prop.Name;
            var value = prop.GetValue(session, null);
            sessionLog += $"\n{name} = {value}";
        }
        _logger.LogInformation(sessionLog);
        return session;
    }
    private async Task<string> GenerateSessionId()
    {
        using var rng = RandomNumberGenerator.Create();
        string userSessionId;
        UserSession? session;
        //thanks copilot ish
        do
        {
            // Generate 16 random bytes (128 bits)
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            // Convert to hex string (32 hex chars)
            userSessionId = Convert.ToHexString(bytes); // e.g., "A1B2C3D4..."
            session = await _userSessionDb.UserSessions
            .FirstOrDefaultAsync(x => x.Id == userSessionId);

        } while (session is not null);

        return userSessionId;
    }

    public async Task<bool> AssociateToRoku(UserSession userSession)
    {
        var rokuSession = await _rokuSessionDb.RokuSessions.FirstOrDefaultAsync(r => r.SessionCode == userSession.SessionCode);
        if (rokuSession == null)
            return false;

        userSession.RokuId = rokuSession.RokuId;
        userSession.MaxScreenSize = rokuSession.MaxScreenSize;
        await _userSessionDb.SaveChangesAsync();
        _logger.LogInformation("Successfully updated UserSession with RokuId and MaxScreenSize");

        return true;
    }
    public async Task<UserSession?> GetUserSession(string userSessiondId)
    {
        return await _userSessionDb.UserSessions
                        .FirstOrDefaultAsync(s => s.Id == userSessiondId);
    }
    public async Task ExpireUserSession(string userSessionId)
    {
        var userSession = await _userSessionDb.UserSessions.FirstOrDefaultAsync(s => s.Id == userSessionId);

        if (userSession is null)
            return;

        userSession.Expired = true;

        await _userSessionDb.SaveChangesAsync();
    }
}