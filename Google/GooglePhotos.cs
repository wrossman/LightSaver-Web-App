using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

public class GooglePhotosFlow
{
    public Dictionary<string, string> FileUrls { get; set; } = new();

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
        System.Console.WriteLine(userSessionId + " Found in StartGoogle Photos flow");
        var session = await _userSessionDb.Sessions.FindAsync(userSessionId);
        string? accessToken = session?.AccessToken;
        string? rokuId = session?.RokuId;
        if (accessToken is null || rokuId is null)
            throw new ArgumentException("Failed to located User Session");

        PickerSession pickerSession = await GetPickerSession(accessToken);
        // CREATE POLLING INSTANCE FOR PICKERSESSION
        if (pickerSession.PickerUri == string.Empty || pickerSession.PollingConfig.PollInterval == string.Empty)
            throw new ArgumentException("Failed to retrieve Picker URI");

        string pickerUri = pickerSession.PickerUri;

        using (var scope = _serviceProvider.CreateScope())
        {
            _ = Task.Run(() => PollPhotos(pickerSession, accessToken, sessionCode, rokuId));
        }
        return pickerUri;

    }
    public async Task WritePhotosToLocal(string accessToken)
    {
        using HttpClient client = new();
        string folderPath = @"C:\Users\billuswillus\Desktop\";
        int filename = 0;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        foreach (KeyValuePair<string, string> item in FileUrls)
        {
            filename++;
            var filePath = folderPath + "google" + filename.ToString() + "." + item.Value;
            using var response = await client.GetAsync(item.Key, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            await responseStream.CopyToAsync(fileStream);
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

            ImageShare share = new(hash, key, sessionCode, data, DateTime.UtcNow, item.Value, rokuId);
            GlobalStore.AddResource(share);
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
    public static async Task<string> GetPhotoList(PickerSession pickerSession, string accessToken)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        string response = await client.GetStringAsync($"https://photospicker.googleapis.com/v1/mediaItems?sessionId={pickerSession.Id}");
        return response;
    }
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
    public async Task<PickerSession> GetPickerSession(string accessToken)
    {
        using HttpClient photosClient = new();

        var pickerRequest = new HttpRequestMessage(HttpMethod.Post, "https://photospicker.googleapis.com/v1/sessions");
        pickerRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

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