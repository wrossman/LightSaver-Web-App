using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
public class LightroomUpdateSessions
{
    private readonly ILogger<LightroomUpdateSessions> _logger;
    private readonly IMemoryCache _sessionCache;
    public static ConcurrentQueue<string> SessionsReadyForTransfer { get; set; } = new();
    public LightroomUpdateSessions(ILogger<LightroomUpdateSessions> logger, IMemoryCache sessionCache)
    {
        _logger = logger;
        _sessionCache = sessionCache;
    }
    public T? GetSession<T>(Guid id)
    {
        return _sessionCache.Get<T>($"{typeof(T).Name}:{id}");
    }
    public T SetSession<T>(Guid id, T session)
    {
        return _sessionCache.Set(Key<T>(id), session, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });
    }
    public string Key<T>(Guid id)
    {
        return $"{typeof(T).Name}:{id}";
    }
    public void RemoveSession<T>(Guid id)
    {
        _sessionCache.Remove(id);
    }
    public Guid CreateUpdateSession(string rokuId)
    {
        Guid id = Guid.NewGuid();
        LightroomUpdateSession session = new()
        {
            Id = id,
            RokuId = rokuId,
            ReadyForTransfer = false,
            CreatedAt = DateTime.UtcNow
        };

        SetSession<LightroomUpdateSession>(id, session);

        return id;
    }
    public bool SetReadyToTransfer(Guid id)
    {
        var session = GetSession<LightroomUpdateSession>(id);
        if (session is null)
            return false;

        var updated = session with { ReadyForTransfer = true };

        SetSession<LightroomUpdateSession>(id, updated);

        return true;
    }
    public bool CheckReadyForTransfer(Guid id, string rokuId)
    {
        var session = GetSession<LightroomUpdateSession>(id);

        if (session is null)
            return false;

        if (session.RokuId != rokuId)
            return false;

        if (session.ReadyForTransfer == true)
            return true;
        else
            return false;
    }
    public bool WriteLinksToSession(Dictionary<Guid, string> updatePackage, Guid id)
    {
        var session = GetSession<LightroomUpdateSession>(id);
        if (session is null)
            return false;

        var updated = session with { ResourcePackage = updatePackage };

        SetSession<LightroomUpdateSession>(id, updated);

        return true;
    }
    public bool ExpireSession(Guid id)
    {
        var session = GetSession<LightroomUpdateSession>(id);
        if (session is null)
            return false;

        var updated = session with { Expired = true };

        SetSession<LightroomUpdateSession>(id, updated);

        return true;
    }
    public Dictionary<Guid, string>? GetResourcePackage(Guid id, string key, string rokuId)
    {
        var session = GetSession<LightroomUpdateSession>(id);

        if (session is null)
            return null;

        if (session.RokuId != rokuId || session.Key != key)
            return null;
        else
            return session.ResourcePackage;
    }
}