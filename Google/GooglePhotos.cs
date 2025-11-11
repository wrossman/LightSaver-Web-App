using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class GooglePhotos
{
    public Dictionary<string, string> FileUrls { get; set; } = new();

    public async Task StartGooglePhotosFlow((PickerSession, PollingConfig) pickerSession, string accessToken, IServiceProvider serviceProvider, IConfiguration config)
    {
        using var scope = serviceProvider.CreateScope();

        string photoList = "";
        if (await PollPhotos(pickerSession, accessToken) == "done")
            photoList = await GooglePhotos.GetPhotoList(pickerSession, accessToken);
        else
        // LOGIC TO END SESSION WITH FAIL
        { }
        ;

        AddUrlsToList(photoList, config);

        await WritePhotosToLocal(pickerSession, accessToken);

    }
    public async Task WritePhotosToLocal((PickerSession, PollingConfig) pickerSession, string accessToken)
    {
        using HttpClient client = new();
        string folderPath = @"C:\Users\billuswillus\Desktop\";
        int filename = 0;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        foreach (KeyValuePair<string, string> item in FileUrls)
        {
            filename++;
            var filePath = folderPath + filename.ToString() + "." + item.Value;
            using var response = await client.GetAsync(item.Key, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            await responseStream.CopyToAsync(fileStream);
        }
    }
    public void AddUrlsToList(string photoList, IConfiguration config)
    {
        MediaItemsResponse photoListJson = JsonSerializer.Deserialize<MediaItemsResponse>(photoList) ?? new();
        List<MediaItem> mediaItems = photoListJson.MediaItems;
        string maxSize = config["MaxPhotoDimensions"] ?? "w3840-h2160";

        foreach (MediaItem item in mediaItems)
        {
            string fileType = item.MediaFile.MimeType;
            fileType = fileType.Substring(fileType.IndexOf("/") + 1);
            System.Console.WriteLine(fileType);
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

        foreach (KeyValuePair<string, string> item in FileUrls)
        {
            System.Console.WriteLine($"{item.Key}: {item.Value}");
        }
    }
    public static async Task<string> GetPhotoList((PickerSession, PollingConfig) pickerSession, string accessToken)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        string response = await client.GetStringAsync($"https://photospicker.googleapis.com/v1/mediaItems?sessionId={pickerSession.Item1.Id}");
        return response;
    }
    public static async Task<string> PollPhotos((PickerSession, PollingConfig) pickerSession, string accessToken)
    {
        int interval;
        decimal timeoutDecimal;
        if (!Int32.TryParse(pickerSession.Item2.PollInterval, out interval))
            interval = 5;
        if (!Decimal.TryParse(pickerSession.Item2.TimeoutIn, out timeoutDecimal))
            timeoutDecimal = 1800;

        int timeout = (int)timeoutDecimal;
        DateTime sessionStartTime = DateTime.Now;
        string sessionId = pickerSession.Item1.Id;
        using HttpClient pollClient = new();

        while (true)
        {
            await Task.Delay(interval * 1000);
            pollClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await pollClient.GetStringAsync($"https://photospicker.googleapis.com/v1/sessions/{sessionId}");
            System.Console.WriteLine(response);
            var responseJson = JsonSerializer.Deserialize<PickerSession>(response);
            if (responseJson is null)
            {
                System.Console.WriteLine("Failed to get PickingSession");

                return "failed";
            }
            ;
            if (responseJson.MediaItemsSet == true)
            {
                System.Console.WriteLine("User clicked DONE.");
                return "done";
            }
            ;
            if (DateTime.Now >= sessionStartTime.AddSeconds(timeout))
            {
                System.Console.WriteLine("Timeout reached.");
                return "timeout";
            }
        }
    }
    public static async Task<(PickerSession?, PollingConfig)?> GetPickerSession(HttpContext context, IConfiguration config, string accessToken)
    {
        using HttpClient photosClient = new();

        var pickerRequest = new HttpRequestMessage(HttpMethod.Post, "https://photospicker.googleapis.com/v1/sessions");
        pickerRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var body = new
        {
            pickingConfig = new
            {
                maxItemCount = config["MaxGooglePhotosItems"]
            }
        };

        string jsonBody = JsonSerializer.Serialize(body);
        pickerRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var pickerResponse = await photosClient.SendAsync(pickerRequest);
        var pickerContent = await pickerResponse.Content.ReadAsStringAsync();

        System.Console.WriteLine(pickerContent);

        var pickerJson = JsonSerializer.Deserialize<PickerSession>(pickerContent);
        if (pickerJson is null || string.IsNullOrEmpty(pickerJson.PickerUri))
            return null;

        var pollingJson = JsonSerializer.Deserialize<PollingConfig>(pickerContent);
        if (pollingJson is null)
            return null;

        return (pickerJson, pollingJson);
    }
}