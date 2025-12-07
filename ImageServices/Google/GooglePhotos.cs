using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class GooglePhotosFlow
{
    private readonly ILogger<GooglePhotosFlow> _logger;
    private readonly IConfiguration _config;
    private readonly LinkSessions _linkSessions;
    private readonly HmacService _hmacService;
    private readonly IServiceScopeFactory _scopeFactory;
    public GooglePhotosFlow(ILogger<GooglePhotosFlow> logger, IConfiguration config, IServiceScopeFactory scopeFactory, LinkSessions linkSessions, HmacService hmacService)
    {
        _logger = logger;
        _hmacService = hmacService;
        _config = config;
        _scopeFactory = scopeFactory;
        _linkSessions = linkSessions;
    }
    public async Task<string> StartGooglePhotosFlow(Guid sessionId)
    {
        PickerSession? pickerSession = await GetPickerSession(sessionId);

        // CREATE POLLING INSTANCE FOR PICKERSESSION
        if (pickerSession is null || pickerSession.PickerUri == string.Empty || pickerSession.PollingConfig.PollInterval == string.Empty)
            throw new ArgumentException("Failed to retrieve Picker URI");

        string pickerUri = pickerSession.PickerUri;

        _ = Task.Run(async () =>
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<GlobalStoreHelpers>();
                var linkSessions = scope.ServiceProvider.GetRequiredService<LinkSessions>();
                if (store is null)
                {
                    _logger.LogWarning("Failed to start polling services for Google Photos");
                    return;
                }
                GooglePhotosPoller poller = new(_config, _logger, store, linkSessions, _hmacService);
                await poller.PollPhotos(pickerSession, sessionId);
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
    public async Task<PickerSession?> GetPickerSession(Guid sessionId)
    {
        var session = _linkSessions.GetSession<LinkSession>(sessionId);
        if (session is null)
        {
            _logger.LogWarning($"Failed to get picker session for user session {sessionId}");
            return null;
        }

        string accessToken = session.AccessToken;
        using HttpClient photosClient = new();

        var pickerRequest = new HttpRequestMessage(HttpMethod.Post, "https://photospicker.googleapis.com/v1/sessions");
        pickerRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var body = new
        {
            pickingConfig = new
            {
                maxItemCount = _config["MaxImages"]
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