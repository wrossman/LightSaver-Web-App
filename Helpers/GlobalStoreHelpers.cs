using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
public class GlobalStoreHelpers
{
    private readonly ILogger<GlobalStoreHelpers> _logger;
    private readonly GlobalImageStoreDbContext _resourceDb;
    public GlobalStoreHelpers(GlobalImageStoreDbContext resourceDb, ILogger<GlobalStoreHelpers> logger)
    {
        _logger = logger;
        _resourceDb = resourceDb;
    }
    public Dictionary<string, string>? GetResourcePackage(string sessionCode, string rokuId)
    {
        var links = _resourceDb.Resources
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
    public async Task WriteResourceToStore(ImageShare resource)
    {
        _resourceDb.Resources.Add(resource);
        await _resourceDb.SaveChangesAsync();
    }
    public async Task RemoveByRokuId(string rokuId)
    {
        var itemsToRemove = await _resourceDb.Resources
        .Where(x => x.RokuId == rokuId)
        .ToListAsync();

        _resourceDb.Resources.RemoveRange(itemsToRemove);
        await _resourceDb.SaveChangesAsync();

        foreach (var item in itemsToRemove)
        {
            _logger.LogInformation("Removed " + item.Id + " from resource database.");
        }
    }
    public (byte[]? image, string? fileType) GetResourceData(string location, string key, string device)
    {
        var item = _resourceDb.Resources
        .Where(img => img.Id == location && img.Key == key && img.RokuId == device)
        .Select(img => img).SingleOrDefault();

        if (item is not null)
            return (item.ImageStream, item.FileType);
        else
            return (null, null);

    }
    public string GetResourceSource(string location, string key, string device)
    {
        var item = _resourceDb.Resources
        .Where(img => img.Id == location && img.Key == key && img.RokuId == device)
        .Select(img => img.Source).SingleOrDefault();

        if (string.IsNullOrEmpty(item))
            return "Unkown";

        return item;
    }
    public string GetResourceLightroomAlbum(string location, string key, string device)
    {
        var item = _resourceDb.Resources
        .Where(img => img.Id == location && img.Key == key && img.RokuId == device)
        .Select(img => img.LightroomAlbum).SingleOrDefault();

        if (string.IsNullOrEmpty(item))
            return "Unkown";

        return item;
    }
    public async Task<bool> ScrubSessionCode(string sessionCode)
    {
        var sessions = await _resourceDb.Resources
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

            await _resourceDb.SaveChangesAsync();
            return true;
        }
    }
    public void WritePhotosToLocal(byte[] img, string fileType)
    {
        string folderPath = @"C:\Users\billuswillus\Desktop\";
        var filePath = folderPath + "img" + DateTime.UtcNow.ToString("HHmmssfff") + "." + fileType;

        using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            fs.Write(img, 0, img.Length);
        }

    }
    public async Task<RevokeAccessPackage> RevokeResourcePackage(RevokeAccessPackage revokePackage)
    {
        string rokuId = revokePackage.RokuId;
        var links = revokePackage.Links;

        var failedRevoke = new RevokeAccessPackage();
        failedRevoke.RokuId = rokuId;

        foreach (var item in links)
        {
            var session = await _resourceDb.Resources
            .FirstOrDefaultAsync(x => x.Key == item.Value && x.Id == item.Key && x.RokuId == rokuId);

            if (session == null)
            {
                failedRevoke.Links.Add(item.Key, item.Value);
                continue;
            }

            _resourceDb.Resources.Remove(session);
            await _resourceDb.SaveChangesAsync();
        }

        return failedRevoke;
    }
    public async Task RemoveByLightroomAlbum(string albumUrl)
    {
        var items = await _resourceDb.Resources
        .Where(x => x.LightroomAlbum == albumUrl)
        .ToListAsync();

        _resourceDb.Resources.RemoveRange(items);
        await _resourceDb.SaveChangesAsync();
    }
    public async Task<List<string>?> GetLightroomOriginUrlsByRokuId(string rokuId)
    {
        return await _resourceDb.Resources
            .Where(x => x.RokuId == rokuId &&
                        x.Source == "lightroom")
            .Select(x => x.OriginUrl)
            .ToListAsync();
    }
    public async Task RemoveByOriginUrls(List<string>? originUrls)
    {
        if (originUrls is null)
            return;

        var itemsToRemove = await _resourceDb.Resources
        .Where(x => originUrls.Contains(x.OriginUrl))
        .ToListAsync();

        _resourceDb.Resources.RemoveRange(itemsToRemove);
        await _resourceDb.SaveChangesAsync();

        _logger.LogInformation(
        "Removed the following URLs from resource database: {RemovedUrls}",
        string.Join("\n", itemsToRemove.Select(i => i.OriginUrl)));
    }
}