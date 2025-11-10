using System.Text.Json;
public class GoogleOAuth
{

    public static async Task<GoogleTokenResponse?> GetAccessToken(HttpContext context, IConfiguration config, string code)
    {
        string clientId = config["OAuth:ClientId"] ?? string.Empty;
        string clientSecret = config["OAuth:ClientSecret"] ?? string.Empty;
        string retrieveTokenUrl = "https://oauth2.googleapis.com/token";

        using HttpClient client = new();

        var payload = new Dictionary<string, string>
    {
        { "client_id", clientId },
        { "client_secret", clientSecret},
        { "code", code },
        { "grant_type", "authorization_code" },
        { "redirect_uri", "https://localhost:8443/auth/google-callback" }
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