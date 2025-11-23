using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class GooglePhotosFlow
{
    private readonly ILogger<GooglePhotosFlow> _logger;
    private readonly IConfiguration _config;
    private readonly UserSessionDbContext _userSessionDb;
    private readonly IServiceProvider _serviceProvider;
    public GooglePhotosFlow(ILogger<GooglePhotosFlow> logger, IConfiguration config, UserSessionDbContext userSessionDb, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _config = config;
        _userSessionDb = userSessionDb;
        _serviceProvider = serviceProvider;
    }
    public async Task<string> StartGooglePhotosFlow(string userSessionId, string sessionCode)
    {
        var session = await _userSessionDb.Sessions.FindAsync(userSessionId);
        string? accessToken = session?.AccessToken;
        string? rokuId = session?.RokuId;
        if (accessToken is null || rokuId is null)
            throw new ArgumentException("Failed to locate User Session");

        PickerSession pickerSession = await GetPickerSession(accessToken);
        // CREATE POLLING INSTANCE FOR PICKERSESSION
        if (pickerSession.PickerUri == string.Empty || pickerSession.PollingConfig.PollInterval == string.Empty)
            throw new ArgumentException("Failed to retrieve Picker URI");

        string pickerUri = pickerSession.PickerUri;

        using (var scope = _serviceProvider.CreateScope())
        {
            _ = Task.Run(() =>
            {
                var options = new DbContextOptionsBuilder<GlobalImageStoreDbContext>().UseInMemoryDatabase("GlobalImageStore").Options;
                GlobalImageStoreDbContext resourceDbContext = new(options);
                GooglePhotosPoller poller = new(_config, _logger, resourceDbContext);
                _ = poller.PollPhotos(pickerSession, accessToken, sessionCode, rokuId);
            });
        }
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
    public async Task<PickerSession> GetPickerSession(string accessToken)
    {
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