using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
public class GoogleFlow
{
    public GoogleFlow(ILogger<GoogleFlow> logger, IConfiguration config, UserSessionDbContext userSessionDb)
    {
        _logger = logger;
        _config = config;
        _userSessionDb = userSessionDb;
    }
    private readonly ILogger<GoogleFlow> _logger;
    private readonly IConfiguration _config;
    private readonly UserSessionDbContext _userSessionDb;
    public async Task<string> GoogleAuthFlow(IPAddress ipAddress, string authCodeString, UserSessions user)
    {

        GoogleTokenResponse? accessTokenJson = await GetAccessToken(authCodeString);
        // ADD TRACKING FOR ACCESSTOKENS
        if (accessTokenJson is null)
            throw new ArgumentException("Failed to retrieve Access Token");
        string accessToken = accessTokenJson.AccessToken;

        string userSessionId = await user.CreateGoogleUserSession(ipAddress, accessToken);
        if (string.IsNullOrEmpty(userSessionId))
        {
            _logger.LogWarning("Failed to create user");
            // add cleanup logic?
            throw new ArgumentException("Failed to create User Session");
        }

        return userSessionId;
    }
    public async Task<GoogleTokenResponse?> GetAccessToken(string code)
    {
        string clientId = _config["OAuth:ClientId"] ?? string.Empty;
        string clientSecret = _config["OAuth:ClientSecret"] ?? string.Empty;
        string retrieveTokenUrl = "https://oauth2.googleapis.com/token";
        string redirectUri = _config["OAuth:RedirectUri"] ?? string.Empty;

        using HttpClient client = new();

        var payload = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret},
            { "code", code },
            { "grant_type", "authorization_code" },
            { "redirect_uri", redirectUri }
        };

        var postContent = new FormUrlEncodedContent(payload);

        var response = await client.PostAsync(retrieveTokenUrl, postContent);
        var respContent = await response.Content.ReadAsStringAsync();

        var jsonResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(respContent);
        if (jsonResponse is null)
            return null;

        return jsonResponse;
    }
    public async Task<bool> LinkAccessToken(string accessToken, string userSessionId)
    {
        var userSession = await _userSessionDb.Sessions.FirstOrDefaultAsync(u => u.Id == userSessionId);

        if (userSession is null)
            return false;

        userSession.AccessToken = accessToken;

        await _userSessionDb.SaveChangesAsync();

        return true;
    }
}