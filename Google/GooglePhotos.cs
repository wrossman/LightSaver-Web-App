using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Runtime.InteropServices;

public class GooglePhotosFlow
{

    public static Dictionary<string, ImageShare> UserImageShare { get; set; } = new();
    public Dictionary<string, string> FileUrls { get; set; } = new();

    public async Task<string> StartGooglePhotosFlow(IServiceProvider serviceProvider, IConfiguration config, UserSessionDbContext userSessionDb, string userSessionId, string sessionCode)
    {
        System.Console.WriteLine(userSessionId + " Found in StartGoogle Photos flow");
        var session = await userSessionDb.Sessions.FindAsync(userSessionId);
        string? accessToken = session?.AccessToken;
        string? rokuId = session?.RokuId;
        if (accessToken is null || rokuId is null)
            throw new ArgumentException("Failed to located User Session");

        PickerSession pickerSession = await GetPickerSession(config, accessToken);
        // CREATE POLLING INSTANCE FOR PICKERSESSION
        if (pickerSession.PickerUri == string.Empty || pickerSession.PollingConfig.PollInterval == string.Empty)
            throw new ArgumentException("Failed to retrieve Picker URI");

        string pickerUri = pickerSession.PickerUri;

        using (var scope = serviceProvider.CreateScope())
        {
            _ = Task.Run(() => PollPhotos(config, pickerSession, accessToken, sessionCode, rokuId));
        }
        return pickerUri;

    }
    public async Task WritePhotosToLocal(PickerSession pickerSession, string accessToken)
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

    public void AddUrlsToList(string photoList, IConfiguration config)
    {
        MediaItemsResponse photoListJson = JsonSerializer.Deserialize<MediaItemsResponse>(photoList) ?? new();
        List<MediaItem> mediaItems = photoListJson.MediaItems;
        string maxSize = config["MaxPhotoDimensions"] ?? "w3840-h2160";

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
        //print all files
        // foreach (KeyValuePair<string, string> item in FileUrls)
        // {
        //     // 
        //     // System.Console.WriteLine($"{item.Key}: {item.Value}");
        // }
    }
    public static async Task<string> GetPhotoList(PickerSession pickerSession, string accessToken)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        string response = await client.GetStringAsync($"https://photospicker.googleapis.com/v1/mediaItems?sessionId={pickerSession.Id}");
        return response;
    }
    public async Task PollPhotos(IConfiguration config, PickerSession pickerSession, string accessToken, string sessionCode, string rokuId)
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
                System.Console.WriteLine("Failed to get PickingSession");

                // return "failed";
            }
            else if (responseJson.MediaItemsSet == true)
            {
                System.Console.WriteLine("User clicked DONE.");
                string photoList = await GooglePhotosFlow.GetPhotoList(pickerSession, accessToken);

                AddUrlsToList(photoList, config);
                await WritePhotosToMemory(sessionCode, accessToken, rokuId);
                UserSessions.CodesReadyForTransfer.Enqueue(sessionCode);

                break;
            }
            else if (responseJson.MediaItemsSet == false)
            {
                System.Console.WriteLine("Still waiting for user to click done.");
                // return "done";
            }
            else if (DateTime.Now >= sessionStartTime.AddSeconds(timeout))
            {
                System.Console.WriteLine("Timeout reached.");
                // return "timeout";
            }

        }
    }
    public static async Task<PickerSession> GetPickerSession(IConfiguration config, string accessToken)
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

        var pickerJson = JsonSerializer.Deserialize<PickerSession>(pickerContent);
        if (pickerJson is null || string.IsNullOrEmpty(pickerJson.PickerUri))
            return (new PickerSession());


        return pickerJson;
    }
}