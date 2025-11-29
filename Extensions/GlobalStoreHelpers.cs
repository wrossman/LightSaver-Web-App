using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
public class GlobalStoreHelpers
{
    private readonly ILogger<GlobalStoreHelpers> _logger;
    private readonly GlobalImageStoreDbContext _resourceDb;
    private readonly IConfiguration _config;
    public GlobalStoreHelpers(GlobalImageStoreDbContext resourceDb, ILogger<GlobalStoreHelpers> logger, IConfiguration config)
    {
        _logger = logger;
        _resourceDb = resourceDb;
        _config = config;
    }
    public Dictionary<string, string>? GetResourcePackage(RokuSession rokuSession)
    {
        string sessionCode = rokuSession.SessionCode;
        string rokuId = rokuSession.RokuId;

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
    public async Task WriteResourceToStore(ImageShare resource, int maxScreenSize)
    {
        resource.ImageStream = ResizeToMaxBox(resource.ImageStream, maxScreenSize);
        _resourceDb.Resources.Add(resource);
        await _resourceDb.SaveChangesAsync();
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
    public string GetResourceSource(ResourceRequest resourceReq)
    {
        string location = resourceReq.Location;
        string key = resourceReq.Key;
        string rokuId = resourceReq.RokuId;

        var item = _resourceDb.Resources
        .Where(img => img.Id == location && img.Key == key && img.RokuId == rokuId)
        .Select(img => img.Source).SingleOrDefault();

        if (string.IsNullOrEmpty(item))
            return "Unkown";

        return item;
    }
    public string GetResourceLightroomAlbum(ResourceRequest resourceReq)
    {
        var item = _resourceDb.Resources
        .Where(img => img.Id == resourceReq.Location && img.Key == resourceReq.Key && img.RokuId == resourceReq.RokuId)
        .Select(img => img.LightroomAlbum).SingleOrDefault();

        if (string.IsNullOrEmpty(item))
            return "Unkown";

        return item;
    }
    public async Task<bool> ScrubOldImages(RokuSession rokuSession)
    {
        string sessionCode = rokuSession.SessionCode;
        string rokuId = rokuSession.RokuId;

        var sessions = await _resourceDb.Resources
            .Where(s => s.RokuId == rokuId && s.SessionCode != sessionCode)
            .ToListAsync();

        if (sessions is null || sessions.Count == 0)
        {
            return false;
        }

        _resourceDb.Resources.RemoveRange(sessions);
        await _resourceDb.SaveChangesAsync();

        return true;
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

        _logger.LogInformation($"Removed {itemsToRemove.Count} lightroom resources from resource database");
    }
    public byte[] ResizeToMaxBox(byte[] input, int maxScreenSize)
    {
        using var image = Image.Load(input);

        _logger.LogInformation($"Checking if image with height: {image.Height} and width: {image.Width} needs resizing.");

        if (image.Width > maxScreenSize || image.Height > maxScreenSize)
        {
            _logger.LogInformation($"Resizing image with dimensions Width: {image.Width} Height: {image.Height}");
            var options = new ResizeOptions
            {
                Mode = ResizeMode.Max, // maintain aspect ratio
                Size = new Size(maxScreenSize, maxScreenSize)
            };

            image.Mutate(x => x.Resize(options));
            _logger.LogInformation($"New dimensions: Width: {image.Width} Height: {image.Height}");
        }

        using var outputStream = new MemoryStream();
        image.Save(outputStream, new JpegEncoder());

        return outputStream.ToArray();
    }
    public async Task<Dictionary<string, string>> GetOldImgsForUpdatePackage(List<string>? imgsToKeep)
    {
        Dictionary<string, string> imgs = new();

        if (imgsToKeep is null)
            return imgs;

        foreach (var item in imgsToKeep)
        {
            var resource = await _resourceDb.Resources
            .FirstOrDefaultAsync(r => r.OriginUrl == item);

            if (resource is null)
                continue;

            imgs.Add(resource.Id, resource.Key);
            _logger.LogInformation($"Added still valid image: {item} to updatePackage");
        }

        return imgs;
    }
}