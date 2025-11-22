using System.Net.Http.Headers;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
public class GooglePhotosPoller
{
    private readonly ILogger<GooglePhotosFlow> _logger;
    private readonly IConfiguration _config;
    private readonly GlobalImageStoreDbContext _resourceDbContext;
    public GooglePhotosPoller(IConfiguration config, ILogger<GooglePhotosFlow> logger, GlobalImageStoreDbContext resourceDbContext)
    {
        _logger = logger;
        _config = config;
        _resourceDbContext = resourceDbContext;
    }
    public Dictionary<string, string> FileUrls { get; set; } = new();

    public async Task PollPhotos(PickerSession pickerSession, string accessToken, string sessionCode, string rokuId)
    {
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
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await pollClient.GetStringAsync($"https://photospicker.googleapis.com/v1/sessions/{sessionId}");
            var responseJson = JsonSerializer.Deserialize<PickerSession>(response);
            if (responseJson is null)
            {
                _logger.LogWarning("Failed to get PickingSession");

                // return "failed";
            }
            else if (responseJson.MediaItemsSet == true)
            {
                _logger.LogInformation("User finished selecting photos.");
                string photoList = await GooglePhotosFlow.GetPhotoList(pickerSession, accessToken);

                AddUrlsToList(photoList);
                await WritePhotosToMemory(sessionCode, accessToken, rokuId);
                UserSessions.CodesReadyForTransfer.Enqueue(sessionCode);

                break;
            }
            else if (responseJson.MediaItemsSet == false)
            {
                _logger.LogInformation("Waiting for user to select photos.");
                // return "done";
            }
            else if (DateTime.Now >= sessionStartTime.AddSeconds(timeout))
            {
                _logger.LogWarning("Timeout reached for photo selection.");
                // return "timeout";
            }

        }
    }
    public void AddUrlsToList(string photoList)
    {
        MediaItemsResponse photoListJson = JsonSerializer.Deserialize<MediaItemsResponse>(photoList) ?? new();
        List<MediaItem> mediaItems = photoListJson.MediaItems;
        string maxSize = _config["MaxPhotoDimensions"] ?? "w3840-h2160";

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
                FileUrls.Add($"{tempFile.BaseUrl}={maxSize}", fileType);
            }
        }
    }
    public async Task WritePhotosToMemory(string sessionCode, string accessToken, string rokuId)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        foreach (KeyValuePair<string, string> item in FileUrls)
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var key = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            byte[] data = await client.GetByteArrayAsync(item.Key);
            string hash = GlobalHelpers.ComputeHashFromBytes(data);
            hash = hash + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

            ImageShare share = new()
            {
                Id = hash,
                Key = key,
                SessionCode = sessionCode,
                ImageStream = data,
                CreatedOn = DateTime.UtcNow,
                FileType = item.Value,
                RokuId = rokuId
            };
            _resourceDbContext.Resources.Add(share);
            await _resourceDbContext.SaveChangesAsync();
        }
    }
}