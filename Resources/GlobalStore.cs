using System.Collections.Concurrent;
using System.Text.Json;

public class GlobalStore
{
    private static ConcurrentBag<ImageShare> GlobalImageStore { get; set; } = new();

    public static Dictionary<string, string>? GetResourcePackage(string sessionCode, string rokuId)
    {
        var links = GlobalImageStore
            .Where(img => img.SessionCode == sessionCode && img.RokuId == rokuId)
            .Select(img => new { img.Id, img.Key })
            .ToDictionary(x => x.Id, x => x.Key);
        if (links is null)
            System.Console.WriteLine("GetResourcePackage Failed");

        foreach (KeyValuePair<string, string> item in links)
        {
            System.Console.WriteLine($"Key: {item.Key} Value: {item.Value}");
        }

        return links;
    }

    public static (byte[] image, string fileType) GetResourceData(string location, string key, string device)
    {
        var test = GlobalImageStore
        .Where(img => img.Id == location && img.Key == key && img.RokuId == device)
        .Select(img => (img.ImageStream, img.FileType)).SingleOrDefault();

        return test;
    }

    public static bool AddResource(ImageShare share)
    {
        try
        {
            GlobalImageStore.Add(share);
        }
        catch (Exception e)
        {
            System.Console.WriteLine($"Failed with exception: {e.Message}");
            return false;
        }
        return true;
    }

}