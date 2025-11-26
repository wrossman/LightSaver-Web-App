using Microsoft.EntityFrameworkCore;
public class SessionHelpers
{
    private readonly ILogger<SessionHelpers> _logger;
    private readonly RokuSessionDbContext _rokuSessionDb;
    private readonly UserSessionDbContext _userSessionDb;
    public SessionHelpers(RokuSessionDbContext rokuSessionDb, UserSessionDbContext userSessionDb, ILogger<SessionHelpers> logger)
    {
        _logger = logger;
        _userSessionDb = userSessionDb;
        _rokuSessionDb = rokuSessionDb;
    }
    public async Task<bool> ExpireUserSession(string sessionCode)
    {
        var session = await _userSessionDb.UserSessions
        .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session != null)
        {
            session.Expired = true;
            session.SessionCode = "";

            await _userSessionDb.SaveChangesAsync();
            return true;
        }
        else
        {
            return false;
        }
    }
    public async Task<bool> ExpireRokuSession(string sessionCode)
    {
        var session = await _rokuSessionDb.RokuSessions
        .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session != null)
        {
            session.Expired = true;
            session.SessionCode = "";

            await _rokuSessionDb.SaveChangesAsync();
            return true;
        }
        else
        {
            return false;
        }
    }
    public async Task<string?> GetSessionCodeFromUserId(string userId)
    {
        return await _userSessionDb.UserSessions
                            .Where(s => s.Id == userId)
                            .Select(s => s.SessionCode)
                            .FirstOrDefaultAsync();
    }
    public async Task<string?> GetRokuIdFromSessionCode(string sessionCode)
    {
        return await _rokuSessionDb.RokuSessions
                        .Where(s => s.SessionCode == sessionCode)
                        .Select(s => s.RokuId)
                        .FirstOrDefaultAsync();
    }
}