using System.Text.Json;
public class GoogleFlow
{
    public async Task<(PickerSession, PollingConfig)> GoogleAuthFlow(HttpContext context, IConfiguration config, string authCodeString, IServiceProvider serviceProvider)
    {

        GoogleTokenResponse? accessTokenJson = await GetAccessToken(context, config, authCodeString);
        // ADD TRACKING FOR ACCESSTOKENS
        if (accessTokenJson is null)
            throw new ArgumentException("Failed to retrieve Access Token");
        string accessToken = accessTokenJson.AccessToken;

        (PickerSession, PollingConfig) pickerSession = ((PickerSession, PollingConfig))await GooglePhotosFlow.GetPickerSession(context, config, accessToken);
        // CREATE POLLING INSTANCE FOR PICKERSESSION
        if (pickerSession.Item1.Id == string.Empty || pickerSession.Item2.PollInterval == string.Empty)
            throw new ArgumentException("Failed to retrieve Picker URI");

        return pickerSession;
    }

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