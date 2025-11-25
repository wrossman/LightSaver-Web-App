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
        var session = await _userSessionDb.Sessions
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
        var session = await _rokuSessionDb.Sessions
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
}