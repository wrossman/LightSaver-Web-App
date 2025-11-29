using System.Collections.Concurrent;
using System.Security.Cryptography;
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
            ReadyForTransfer = false
        };
        _updateSessionDb.UpdateSessions.Add(session);
        await _updateSessionDb.SaveChangesAsync();
        return session;
    }
    public void SetReadyForTransfer(LightroomUpdateSession session)
    {
        _updateSessionDb.UpdateSessions.Attach(session);
        _updateSessionDb.Entry(session).State = EntityState.Modified;
        _updateSessionDb.SaveChanges();
    }
    public async Task<LightroomUpdateSession?> GetUpdateSession(string key, string rokuId)
    {
        var session = await _updateSessionDb.UpdateSessions.FirstOrDefaultAsync(s => s.Id == key && s.RokuId == rokuId);

        return session;
    }
}