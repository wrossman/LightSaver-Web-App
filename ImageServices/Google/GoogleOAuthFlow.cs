using System.Text.Json;
public class GoogleOAuthFlow
{
    public GoogleOAuthFlow(ILogger<GoogleOAuthFlow> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }
    private readonly ILogger<GoogleOAuthFlow> _logger;
    private readonly IConfiguration _config;
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
        {
            _logger.LogWarning("Received an empty response from google oauth server");
            return null;
        }
        return jsonResponse;
    }
}