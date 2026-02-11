using System.Security.Cryptography;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Authentication;
public class LinkSessions
{
    private readonly ILogger<LinkSessions> _logger;
    private readonly IMemoryCache _sessionCache;
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
    public Guid SetSessionCodeSession(string id, Guid guid)
    {
        return _sessionCache.Set(id, guid, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });
    }
    public Guid GetSessionCodeSession(string id)
    {
        return _sessionCache.Get<Guid>(id);
    }
    public string Key<T>(Guid id)
    {
        return $"{typeof(T).Name}:{id}";
    }
    public void RemoveSession<T>(Guid id)
    {
        _sessionCache.Remove(Key<T>(id));
    }
    public bool LinkAccessToken(string accessToken, Guid id)
    {
        var session = GetSession<LinkSession>(id);
        if (session is null)
            return false;

        var updated = session with { AccessToken = accessToken };

        SetSession<LinkSession>(id, updated);

        return true;
    }
    public bool SetResourcePackage(Guid id, List<string> imageServiceLinks)
    {
        var session = GetSession<LinkSession>(id);
        if (session is null)
            return false;

        var updated = session with { ImageServiceLinks = imageServiceLinks };

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

        return session.ReadyForTransfer;
    }
    public bool CheckExpired(Guid id, string rokuId, string sessionCode)
    {
        var session = GetSession<LinkSession>(id);

        if (session is null)
            return true;

        if (session.RokuId != rokuId)
            return true;

        if (session.SessionCode != sessionCode)
            return true;

        return session.Expired;
    }
    public int GetDownloadedResourceCount(Guid sessionId)
    {
        return GetSession<LinkSession>(sessionId).ResourcesSaved;
    }
    public bool ExpireSession(Guid id)
    {
        var session = GetSession<LinkSession>(id);
        if (session is null)
            return false;

        var updated = session with { Expired = true };

        SetSession<LinkSession>(id, updated);

        var sessionCode = session.SessionCode;
        _sessionCache.Remove(sessionCode);

        return true;
    }
    public string GetSessionCodeFromSession(Guid id)
    {
        var session = GetSession<LinkSession>(id);

        if (session is null)
        {
            _logger.LogWarning("Tried to access session code of null session");
            throw new InvalidOperationException();
        }

        return session?.SessionCode ?? "";
    }
    public Dictionary<Guid, string> GetResourcePackage(Guid id, string sessionCode, string rokuId)
    {
        var session = GetSession<LinkSession>(id);

        if (session is null || session.RokuId != rokuId || session.SessionCode != sessionCode)
            throw new AuthenticationException();
        else
            return session.ResourcePackage;
    }
    public Guid CreateLinkSession(IPAddress ipAddress, string rokuId, int screenWidth, int screenHeight)
    {
        Guid id = Guid.NewGuid();
        string sessionCode;

        do
        {
            sessionCode = GenerateSessionCode();
        } while (_sessionCache.TryGetValue(sessionCode, out var _));

        LinkSession session = new()
        {
            Id = id,
            RokuId = rokuId,
            CreatedAt = DateTime.UtcNow,
            SourceAddress = ipAddress.ToString(),
            SessionCode = sessionCode,
            ReadyForTransfer = false,
            Expired = false,
            ScreenWidth = screenWidth,
            ScreenHeight = screenHeight
        };

        SetSession<LinkSession>(id, session);
        SetSessionCodeSession(sessionCode, id);

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