using System.Net;
using System.Text.Json;
public class GoogleFlow
{
    public static async Task<string> GoogleAuthFlow(IPAddress ipAddress, HttpContext context, IConfiguration config, string authCodeString, UserSessionDbContext userSessionDbContext)
    {

        GoogleTokenResponse? accessTokenJson = await GetAccessToken(context, config, authCodeString);
        // ADD TRACKING FOR ACCESSTOKENS
        if (accessTokenJson is null)
            throw new ArgumentException("Failed to retrieve Access Token");
        string accessToken = accessTokenJson.AccessToken;

        string userSessionId = await UserSessions.CreateUserSession(ipAddress, userSessionDbContext, accessToken);
        if (string.IsNullOrEmpty(userSessionId))
        {
            Console.WriteLine("Failed to create user");
            // add cleanup logic?
            throw new ArgumentException("Failed to create User Session");
        }
        ;

        return userSessionId;

    }


    public static async Task<GoogleTokenResponse?> GetAccessToken(HttpContext context, IConfiguration config, string code)
    {
        string clientId = config["OAuth:ClientId"] ?? string.Empty;
        string clientSecret = config["OAuth:ClientSecret"] ?? string.Empty;
        string retrieveTokenUrl = "https://oauth2.googleapis.com/token";
        string redirectUri = config["OAuth:RedirectUri"] ?? string.Empty;

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
}