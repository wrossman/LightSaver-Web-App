using Microsoft.EntityFrameworkCore;
public class GlobalStoreHelpers
{
    public static Dictionary<string, string>? GetResourcePackage(GlobalImageStoreDbContext resourceDbContext, string sessionCode, string rokuId)
    {
        var links = resourceDbContext.Resources
            .Where(img => img.SessionCode == sessionCode && img.RokuId == rokuId)
            .ToDictionary(img => img.Id, img => img.Key);
        if (links is null)
        {
            return null;
        }

        string resourcePackage = "\n";
        foreach (KeyValuePair<string, string> item in links)
        {
            resourcePackage += $"Key: {item.Key} Value: {item.Value}\n";
        }

        return links;
    }

    public static (byte[]? image, string? fileType) GetResourceData(GlobalImageStoreDbContext resourceDbContext, string location, string key, string device)
    {
        var test = resourceDbContext.Resources
        .Where(img => img.Id == location && img.Key == key && img.RokuId == device)
        .Select(img => img).SingleOrDefault();

        if (test is not null)
            return (test.ImageStream, test.FileType);
        else
            return (null, null);

    }

    public static async Task<bool> ScrubSessionCode(GlobalImageStoreDbContext resourceDb, string sessionCode)
    {
        var sessions = await resourceDb.Resources
        .Where(s => s.SessionCode == sessionCode)
        .ToListAsync();
        if (sessions is null)
        {
            return false;
        }
        else
        {
            foreach (var s in sessions)
                s.SessionCode = string.Empty;

            await resourceDb.SaveChangesAsync();
            return true;
        }
    }
}