using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography;
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
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
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
    public (Guid, string) CreateUpdateSession(string rokuId)
    {
        Guid id = Guid.NewGuid();

        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var key = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var keyDerivation = Pbkdf2Hasher.Hash(key);

        LightroomUpdateSession session = new()
        {
            Id = id,
            Key = keyDerivation,
            RokuId = rokuId,
            ReadyForTransfer = false,
            CreatedAt = DateTime.UtcNow
        };

        SetSession<LightroomUpdateSession>(id, session);

        return (id, key);
    }
    public bool SetReadyToTransfer(Guid id)
    {
        var session = GetSession<LightroomUpdateSession>(id);
        if (session is null)
        {
            _logger.LogInformation("Set ready to transfer session was null");
            return false;
        }
        var updated = session with { ReadyForTransfer = true };

        SetSession<LightroomUpdateSession>(id, updated);
        _logger.LogInformation($"Set ready to transfer for session {id}");
        return true;
    }
    public bool CheckReadyForTransfer(Guid id, string rokuId, string key)
    {
        var session = GetSession<LightroomUpdateSession>(id);

        if (session is null || session.RokuId != rokuId || !Pbkdf2Hasher.Verify(key, session.Key))
        {
            throw new AuthenticationException();
        }

        return session.ReadyForTransfer;

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
        {
            _logger.LogWarning($"Failed to expire session with id {id} due to session not found.");
            return false;
        }

        var updated = session with { Expired = true };

        SetSession<LightroomUpdateSession>(id, updated);

        return true;
    }
    public Dictionary<Guid, string>? GetResourcePackage(Guid id, string key, string rokuId)
    {
        var session = GetSession<LightroomUpdateSession>(id);

        if (session is null || session.RokuId != rokuId || !Pbkdf2Hasher.Verify(key, session.Key))
            throw new AuthenticationException();

        return session.ResourcePackage;

    }
}