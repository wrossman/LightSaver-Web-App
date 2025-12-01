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
    public async Task<bool> ExpireSessionsBySessionCode(string sessionCode)
    {
        var rokuSession = await _rokuSessionDb.RokuSessions
        .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        var userSession = await _userSessionDb.UserSessions
        .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (rokuSession != null && userSession != null)
        {
            rokuSession.Expired = true;
            await _rokuSessionDb.SaveChangesAsync();

            userSession.Expired = true;
            await _userSessionDb.SaveChangesAsync();
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
    public async Task SetReadyToTransfer(string sessionCode)
    {
        var userSession = await _userSessionDb.UserSessions
                            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        if (userSession != null)
        {
            userSession.ReadyForTransfer = true;
            await _userSessionDb.SaveChangesAsync();
        }
        else
        {
            _logger.LogWarning($"TestSessionCode Failed getting userSession for session code {sessionCode}");
        }

        var rokuSession = await _rokuSessionDb.RokuSessions
        .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        if (rokuSession != null)
        {
            rokuSession.ReadyForTransfer = true;
            await _rokuSessionDb.SaveChangesAsync();
        }
        else
        {
            _logger.LogWarning($"TestSessionCode Failed getting rokuSession for session code {sessionCode}");
        }
    }
}