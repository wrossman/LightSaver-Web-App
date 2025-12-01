using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
public class LightrooomUpdateSessions
{
    private readonly ILogger<LightrooomUpdateSessions> _logger;
    private readonly LightroomUpdateSessionDbContext _updateSessionDb;
    public static ConcurrentQueue<string> SessionsReadyForTransfer { get; set; } = new();
    public LightrooomUpdateSessions(ILogger<LightrooomUpdateSessions> logger, LightroomUpdateSessionDbContext updateSessionDb)
    {
        _logger = logger;
        _updateSessionDb = updateSessionDb;
    }
    public async Task<LightroomUpdateSession> CreateUpdateSession(string rokuId)
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var key = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        LightroomUpdateSession session = new()
        {
            Id = key,
            RokuId = rokuId,
            ReadyForTransfer = false,
            CreatedAt = DateTime.UtcNow
        };
        _updateSessionDb.UpdateSessions.Add(session);
        await _updateSessionDb.SaveChangesAsync();
        return session;
    }
    public async Task SetReadyForTransfer(LightroomUpdateSession session)
    {
        var updateSession = await _updateSessionDb.UpdateSessions.FindAsync(session.Id);
        if (updateSession is null)
        {
            _logger.LogWarning($"Failed to set update session as ready for transfer for session id {session.Id}");
            return;
        }

        updateSession.ReadyForTransfer = true;
        await _updateSessionDb.SaveChangesAsync();
    }
    public async Task WriteLinksToSession(Dictionary<string, string> updatePackage, LightroomUpdateSession session)
    {
        var updateSession = await _updateSessionDb.UpdateSessions.FindAsync(session.Id);
        if (updateSession is null)
        {
            _logger.LogWarning($"Failed to set update session links for session id {session.Id}");
            return;
        }

        updateSession.Links = updatePackage;
        await _updateSessionDb.SaveChangesAsync();
    }
    public async Task ExpireUpdateSession(LightroomUpdateSession session)
    {
        var updateSession = await _updateSessionDb.UpdateSessions.FindAsync(session.Id);
        if (updateSession is null)
        {
            _logger.LogWarning($"Failed to set update session as expired for session id {session.Id}");
            return;
        }

        updateSession.Expired = true;
        await _updateSessionDb.SaveChangesAsync();
    }
    public async Task<LightroomUpdateSession?> GetUpdateSession(string key, string rokuId)
    {
        var session = await _updateSessionDb.UpdateSessions.FirstOrDefaultAsync(s => s.Id == key && s.RokuId == rokuId);

        return session;
    }
}