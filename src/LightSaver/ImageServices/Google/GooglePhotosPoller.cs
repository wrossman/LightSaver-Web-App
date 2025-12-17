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
        if (linkSession is null || linkSession.Expired)
        {
            _logger.LogWarning($"Failed to Poll Google Photos for session {linkSessionId}");
            return;
        }

        int interval;
        double timeoutDecimal;
        if (!Int32.TryParse(pickerSession.PollingConfig.PollInterval, out interval))
            interval = 5;
        if (!double.TryParse($"{pickerSession.PollingConfig.TimeoutIn.TrimEnd('s')}", out timeoutDecimal))
            timeoutDecimal = 1800;

        _logger.LogInformation($"Start polling google photos session for {timeoutDecimal} seconds");

        TimeSpan timeout = TimeSpan.FromSeconds(timeoutDecimal);
        DateTime sessionStartTime = DateTime.Now;
        DateTime sessionExpiration = sessionStartTime + timeout;

        string sessionId = pickerSession.Id;
        using HttpClient pollClient = new();

        while (true)
        {
            var session = _linkSessions.GetSession<LinkSession>(linkSessionId);
            if (session is null || session.Expired)
            {
                _logger.LogInformation("Session has expired or images have been transferred.");
                _logger.LogInformation("Stopping google photos polling for.");
                break;
            }

            await Task.Delay(interval * 1000);
            pollClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", linkSession.AccessToken);

            var response = await pollClient.GetStringAsync($"https://photospicker.googleapis.com/v1/sessions/{sessionId}");
            var responseJson = JsonSerializer.Deserialize<PickerSession>(response);
            if (responseJson is null)
            {
                _logger.LogWarning("Failed to get PickingSession");
                break;
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
            else if (DateTime.Now >= sessionExpiration)
            {
                _logger.LogWarning("Timeout reached for photo selection.");
                break;
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
                linkSession.ImageServiceLinks.Add($"{tempFile.BaseUrl}={maxScreenSize}");
            }
        }
        _linkSessions.SetSession<LinkSession>(linkSessionId, linkSession);
    }
}