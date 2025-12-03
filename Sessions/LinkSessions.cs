using System.Security.Cryptography;
using System.Net;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
public class LinkSessions
{
    private readonly ILogger<LinkSessions> _logger;
    private readonly IMemoryCache _sessionCache;
    public static ConcurrentDictionary<string, Guid> SessionCodeMap { get; set; } = new();
    public LinkSessions(ILogger<LinkSessions> logger, IMemoryCache sessionCache)
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
    public async Task<bool> LinkAccessToken(string accessToken, Guid id)
    {
        var session = GetSession<LinkSession>(id);
        if (session is null)
            return false;

        var updated = session with { AccessToken = accessToken };

        SetSession<LinkSession>(id, updated);

        return true;
    }
    public bool SetReadyToTransfer(Guid id)
    {
        var session = GetSession<LinkSession>(id);
        if (session is null)
            return false;

        var updated = session with { ReadyForTransfer = true };

        SetSession<LinkSession>(id, updated);

        return true;
    }
    public bool CheckReadyForTransfer(Guid id, string rokuId, string sessionCode)
    {
        var session = GetSession<LinkSession>(id);

        if (session is null)
            return false;

        if (session.RokuId != rokuId)
            return false;

        if (session.SessionCode != sessionCode)
            return false;

        if (session.ReadyForTransfer == true)
            return true;
        else
            return false;
    }
    public bool ExpireSession(Guid id)
    {
        var session = GetSession<LinkSession>(id);
        if (session is null)
            return false;

        var updated = session with { Expired = true };

        SetSession<LinkSession>(id, updated);

        return true;
    }
    public string GetSessionCodeFromSession(Guid id)
    {
        var session = GetSession<LinkSession>(id);

        if (session is null)
            _logger.LogWarning("Tried to access session code of null session");

        return session?.SessionCode ?? "";
    }
    public Dictionary<Guid, string>? GetResourcePackage(Guid id, string sessionCode, string rokuId)
    {
        var session = GetSession<LinkSession>(id);

        if (session is null)
            return null;

        if (session.RokuId != rokuId || session.SessionCode != sessionCode)
            return null;
        else
            return session.ResourcePackage;
    }
    public Guid CreateLinkSession(IPAddress ipAddress, string rokuId, int maxScreenSize)
    {
        Guid id = Guid.NewGuid();
        string sessionCode;

        do
        {
            sessionCode = GenerateSessionCode();
        } while (!SessionCodeMap.TryAdd(sessionCode, id));

        LinkSession session = new()
        {
            Id = id,
            RokuId = rokuId,
            CreatedAt = DateTime.UtcNow,
            SourceAddress = ipAddress.ToString(),
            SessionCode = sessionCode,
            ReadyForTransfer = false,
            Expired = false,
            MaxScreenSize = maxScreenSize
        };

        SetSession<LinkSession>(id, session);
        return id;
    }
    private static string GenerateSessionCode()
    {
        // thanks chat
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        using var rng = RandomNumberGenerator.Create();

        var bytes = new byte[7];
        rng.GetBytes(bytes);

        char[] result = new char[7];

        for (int i = 0; i < 7; i++)
        {
            // Convert random byte (0–255) into index (0–35)
            result[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(result);
    }
}