using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.VisualBasic;

public class GooglePhotos
{
    public List<string> FileUrls { get; set; } = new();

    public async Task GooglePhotosFlow((PickerSession, PollingConfig) pickerSession, string accessToken, IServiceProvider serviceProvider, IConfiguration config)
    {
        using var scope = serviceProvider.CreateScope();

        string photoList = "";
        if (await PollPhotos(pickerSession, accessToken) == "done")
            photoList = await GooglePhotos.GetPhotoList(pickerSession, accessToken);
        else
        // LOGIC TO END SESSION WITH FAIL
        { }
        ;

        MediaItemsResponse photoListJson = JsonSerializer.Deserialize<MediaItemsResponse>(photoList) ?? new();
        List<MediaItem> mediaItems = photoListJson.MediaItems;
        string maxSize = config["MaxPhotoDimensions"] ?? "w3840-h2160";

        foreach (MediaItem item in mediaItems)
        {
            if (item.Type == "PHOTO")
            {
                MediaFile tempFile = item.MediaFile;
                FileUrls.Add($"{tempFile.BaseUrl}={maxSize}");
            }
        }

        foreach (string item in FileUrls)
        {
            System.Console.WriteLine(item);
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