using System.Net.Http.Headers;
using System.Text.Json;
public class GooglePhotosPoller
{
    private readonly ILogger<GooglePhotosFlow> _logger;
    private readonly IConfiguration _config;
    private readonly GlobalStore _store;
    private readonly LinkSessions _linkSessions;
    public GooglePhotosPoller(IConfiguration config, ILogger<GooglePhotosFlow> logger, GlobalStore store, LinkSessions linkSessions)
    {
        _logger = logger;
        _config = config;
        _store = store;
        _linkSessions = linkSessions;
    }
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

                AddUrlsToSession(photoList, linkSession.Id);
                await _store.WriteSessionImages(linkSessionId, ImageShareSource.Google);
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
    public void AddUrlsToSession(string photoList, Guid linkSessionId)
    {
        var linkSession = _linkSessions.GetSession<LinkSession>(linkSessionId);
        if (linkSession is null)
        {
            _logger.LogWarning($"Failed to get session fo {linkSessionId} at AddUrlsToSession");
            throw new ArgumentNullException();
        }

        MediaItemsResponse photoListJson = JsonSerializer.Deserialize<MediaItemsResponse>(photoList) ?? new();
        List<MediaItem> mediaItems = photoListJson.MediaItems;
        string maxScreenSize = $"w{linkSession.MaxScreenSize}-h{linkSession.MaxScreenSize}";

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
                linkSession.ImageServiceLinks.Add($"{tempFile.BaseUrl}={maxScreenSize}", fileType);
            }
        }
        _linkSessions.SetSession<LinkSession>(linkSessionId, linkSession);
    }
}