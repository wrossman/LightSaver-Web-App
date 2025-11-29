using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class GooglePhotosFlow
{
    private readonly ILogger<GooglePhotosFlow> _logger;
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    public GooglePhotosFlow(ILogger<GooglePhotosFlow> logger, IConfiguration config, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _config = config;
        _scopeFactory = scopeFactory;
    }
    public async Task<string> StartGooglePhotosFlow(UserSession userSession)
    {
        PickerSession pickerSession = await GetPickerSession(userSession);
        // CREATE POLLING INSTANCE FOR PICKERSESSION
        if (pickerSession.PickerUri == string.Empty || pickerSession.PollingConfig.PollInterval == string.Empty)
            throw new ArgumentException("Failed to retrieve Picker URI");

        string pickerUri = pickerSession.PickerUri;

        _ = Task.Run(async () =>
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<GlobalStoreHelpers>();
                if (store is null)
                {
                    _logger.LogWarning("Failed to start polling services for Google Photos");
                    return;
                }
                GooglePhotosPoller poller = new(_config, _logger, store);
                await poller.PollPhotos(pickerSession, userSession);
            }
        });

        return pickerUri;

    }
    public static async Task<string> GetPhotoList(PickerSession pickerSession, string accessToken)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        string response = await client.GetStringAsync($"https://photospicker.googleapis.com/v1/mediaItems?sessionId={pickerSession.Id}");
        return response;
    }
    public async Task<PickerSession> GetPickerSession(UserSession userSession)
    {
        string accessToken = userSession.AccessToken;
        using HttpClient photosClient = new();

        var pickerRequest = new HttpRequestMessage(HttpMethod.Post, "https://photospicker.googleapis.com/v1/sessions");
        pickerRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var body = new
        {
            pickingConfig = new
            {
                maxItemCount = _config["MaxGooglePhotosItems"]
            }
        };

        string jsonBody = JsonSerializer.Serialize(body);
        pickerRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var pickerResponse = await photosClient.SendAsync(pickerRequest);
        var pickerContent = await pickerResponse.Content.ReadAsStringAsync();

        var pickerJson = JsonSerializer.Deserialize<PickerSession>(pickerContent);
        if (pickerJson is null || string.IsNullOrEmpty(pickerJson.PickerUri))
            return new PickerSession();


        return pickerJson;
    }
}