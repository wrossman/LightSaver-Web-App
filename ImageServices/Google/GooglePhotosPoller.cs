using System.Net.Http.Headers;
using System.Text.Json;
using System.Security.Cryptography;
public class GooglePhotosPoller
{
    private readonly ILogger<GooglePhotosFlow> _logger;
    private readonly IConfiguration _config;
    private readonly GlobalStoreHelpers _store;
    private readonly LinkSessions _linkSessions;
    private readonly HmacHelper _hmacService;
    public GooglePhotosPoller(IConfiguration config, ILogger<GooglePhotosFlow> logger, GlobalStoreHelpers store, LinkSessions linkSessions, HmacHelper hmacService)
    {
        _logger = logger;
        _hmacService = hmacService;
        _config = config;
        _store = store;
        _linkSessions = linkSessions;
    }
    public Dictionary<string, string> FileUrls { get; set; } = new();
    public async Task PollPhotos(PickerSession pickerSession, Guid linkSessionId)
    {
        var linkSession = _linkSessions.GetSession<LinkSession>(linkSessionId);
        if (linkSession is null)
        {
            _logger.LogWarning($"Failed to Poll Google Photos for session {linkSessionId}");
            return;
        }

        int interval;
        decimal timeoutDecimal;
        if (!Int32.TryParse(pickerSession.PollingConfig.PollInterval, out interval))
            interval = 5;
        if (!Decimal.TryParse(pickerSession.PollingConfig.TimeoutIn, out timeoutDecimal))
            timeoutDecimal = 1800;

        int timeout = (int)timeoutDecimal;
        DateTime sessionStartTime = DateTime.Now;
        string sessionId = pickerSession.Id;
        using HttpClient pollClient = new();

        while (true)
        {
            await Task.Delay(interval * 1000);
            pollClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", linkSession.AccessToken);

            var response = await pollClient.GetStringAsync($"https://photospicker.googleapis.com/v1/sessions/{sessionId}");
            var responseJson = JsonSerializer.Deserialize<PickerSession>(response);
            if (responseJson is null)
            {
                _logger.LogWarning("Failed to get PickingSession");
            }
            else if (responseJson.MediaItemsSet == true)
            {
                _logger.LogInformation("User finished selecting photos.");
                string photoList = await GooglePhotosFlow.GetPhotoList(pickerSession, linkSession.AccessToken);

                AddUrlsToList(photoList, linkSession);
                await WritePhotosToDb(linkSessionId);
                _linkSessions.SetReadyToTransfer(linkSessionId);

                break;
            }
            else if (responseJson.MediaItemsSet == false)
            {
                _logger.LogInformation("Waiting for user to select photos.");
            }
            else if (DateTime.Now >= sessionStartTime.AddSeconds(timeout))
            {
                _logger.LogWarning("Timeout reached for photo selection.");
            }
        }
    }
    public void AddUrlsToList(string photoList, LinkSession LinkSession)
    {
        MediaItemsResponse photoListJson = JsonSerializer.Deserialize<MediaItemsResponse>(photoList) ?? new();
        List<MediaItem> mediaItems = photoListJson.MediaItems;
        string maxScreenSize = $"w{LinkSession.MaxScreenSize}-h{LinkSession.MaxScreenSize}";

        foreach (MediaItem item in mediaItems)
        {
            string fileType = item.MediaFile.MimeType;
            fileType = fileType.Substring(fileType.IndexOf("/") + 1);
            if (item.Type == "PHOTO" &&
            (string.Equals(fileType, "jpg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileType, "jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileType, "png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileType, "gif", StringComparison.OrdinalIgnoreCase)))
            {
                MediaFile tempFile = item.MediaFile;
                FileUrls.Add($"{tempFile.BaseUrl}={maxScreenSize}", fileType);
            }
        }
    }
    public async Task WritePhotosToDb(Guid linkSessionId)
    {
        var session = _linkSessions.GetSession<LinkSession>(linkSessionId);
        if (session is null)
            return;

        var updatedSession = session with { };

        using HttpClient client = new();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
        foreach (KeyValuePair<string, string> item in FileUrls)
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var key = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var keyDerivation = _hmacService.Hash(key);

            byte[] data = await client.GetByteArrayAsync(item.Key);

            ImageShare share = new()
            {
                Id = Guid.NewGuid(),
                Key = keyDerivation,
                KeyCreated = DateTime.UtcNow,
                SessionCode = session.SessionCode,
                ImageStream = data,
                CreatedOn = DateTime.UtcNow,
                FileType = item.Value,
                RokuId = session.RokuId,
                Source = ImageShareSource.Google,
                Origin = GlobalHelpers.ComputeHashFromString(item.Key)
            };
            await _store.WriteResourceToStore(share, session.MaxScreenSize);
            updatedSession.ResourcePackage.Add(share.Id, key);
        }
        _linkSessions.SetSession<LinkSession>(linkSessionId, updatedSession);
    }
}