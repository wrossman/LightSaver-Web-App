using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using SixLabors.ImageSharp.Formats.Webp;
public class GlobalStore
{
    private readonly ILogger<GlobalStore> _logger;
    private readonly GlobalImageStoreDbContext _resourceDb;
    private readonly IConfiguration _config;
    private readonly HmacHelper _hmacService;
    private readonly LinkSessions _linkSessions;
    private readonly IResourceSave _resourceSave;
    private readonly ImageProcessors _imageProcessors;
    public GlobalStore(ImageProcessors imageProcessors, IResourceSave resourceSave, LinkSessions linkSessions, GlobalImageStoreDbContext resourceDb, ILogger<GlobalStore> logger, IConfiguration config, HmacHelper hmacService)
    {
        _imageProcessors = imageProcessors;
        _resourceSave = resourceSave;
        _logger = logger;
        _resourceDb = resourceDb;
        _config = config;
        _hmacService = hmacService;
        _linkSessions = linkSessions;
    }
    public async Task<byte[]> GetResourceData(Guid id, string key, string device)
    {
        var item = _resourceDb.Resources
        .Where(img => img.Id == id && img.RokuId == device)
        .Select(img => img).SingleOrDefault();

        if (item is null || !_hmacService.Verify(key, item.Key))
        {
            throw new AuthenticationException();
        }

        var img = await _resourceSave.GetResource(item);

        return img;
    }
    public async Task<string?> GetUpdatedKey(Guid id, string key, string device)
    {
        var item = await _resourceDb.Resources
        .SingleOrDefaultAsync(img => img.Id == id && img.RokuId == device);

        if (item is null || !_hmacService.Verify(key, item.Key))
        {
            throw new AuthenticationException();
        }

        var keyExpireDate = item.KeyCreated + TimeSpan.FromDays(30);
        var remaining = keyExpireDate - DateTime.UtcNow;

        if (remaining > TimeSpan.FromDays(7))
        {
            return null;
        }
        else if (remaining <= TimeSpan.Zero)
        {
            _logger.LogWarning("Client tried to retrieve update key for ImageShare with key older than rotation time.");
            await _resourceSave.RemoveSingle(item);
            _resourceDb.Remove(item);
            await _resourceDb.SaveChangesAsync();
            throw new AuthenticationException();
        }

        _logger.LogInformation($"Creating new key for resource id {id.ToString()}");
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var newKey = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var keyDerivation = _hmacService.Hash(newKey);

        item.Key = keyDerivation;
        item.KeyCreated = DateTime.UtcNow;
        await _resourceDb.SaveChangesAsync();

        return newKey;
    }
    public async Task<byte[]?> GetBackgroundData(Guid id, string key, string device, int height, int width)
    {
        byte[] result;
        try
        {
            result = await GetResourceData(id, key, device);
        }
        catch (AuthenticationException)
        {
            _logger.LogWarning("Incorrect keys were tried at the get background data method.");
            return null;
        }
        catch (IOException)
        {
            _logger.LogWarning("Background data was attempted to be retrieved for file that does not exist.");
            return null;
        }

        try
        {
            using var image = Image.Load(result);
            image.Mutate(x =>
                        {
                            x.Resize(width / 8, height / 8);
                            x.GaussianBlur(6);
                            x.Resize(width, height);
                        });
            using var outputStream = new MemoryStream();
            image.Save(outputStream, new WebpEncoder());
            return outputStream.ToArray();
        }
        catch
        {
            _logger.LogWarning("Failed to create background image.");
            return null;
        }
    }
    public ImageShareSource GetResourceSource(ResourceRequest resourceReq)
    {
        var item = _resourceDb.Resources
        .Where(img => img.Id == resourceReq.Id && img.RokuId == resourceReq.RokuId).SingleOrDefault();

        if (item is null || !_hmacService.Verify(resourceReq.Key, item.Key))
        {
            _logger.LogWarning("Failed to verify key against stored hash value in image store.");
            throw new AuthenticationException();
        }

        return item.Source;

    }
    public string GetResourceLightroomAlbum(ResourceRequest resourceReq)
    {
        var item = _resourceDb.Resources
        .Where(img => img.Id == resourceReq.Id && img.RokuId == resourceReq.RokuId).SingleOrDefault();

        if (item is null)
            return "";

        if (_hmacService.Verify(resourceReq.Key, item.Key))
        {
            return item.LightroomAlbum;
        }
        else
        {
            _logger.LogWarning("Failed to verify key against stored hash value in image store.");
            return "";
        }
    }
    public async Task<bool> ScrubOldImages(string rokuId, string sessionCode)
    {
        var sessions = await _resourceDb.Resources
            .Where(s => s.RokuId == rokuId && s.SessionCode != sessionCode)
            .ToListAsync();

        if (sessions is null || sessions.Count == 0)
        {
            return false;
        }

        await _resourceSave.RemoveList(sessions);

        _resourceDb.Resources.RemoveRange(sessions);
        await _resourceDb.SaveChangesAsync();

        return true;
    }
    public async Task<RevokeAccessPackage> RevokeResourcePackage(RevokeAccessPackage revokePackage)
    {
        string rokuId = revokePackage.RokuId;
        var links = revokePackage.Links;

        var failedRevoke = new RevokeAccessPackage();
        failedRevoke.RokuId = rokuId;

        List<ImageShare> toRemove = new();

        foreach (var item in links)
        {
            var resource = await _resourceDb.Resources
            .FirstOrDefaultAsync(x => x.Id == item.Key && x.RokuId == rokuId);

            if (resource == null)
            {
                failedRevoke.Links.Add(item.Key, item.Value);
                continue;
            }

            if (!_hmacService.Verify(item.Value, resource.Key))
            {
                _logger.LogWarning("An incorrect key was passed in a revoke resource package.");
                failedRevoke.Links.Add(item.Key, item.Value);
                continue;
            }

            toRemove.Add(resource);
        }

        _logger.LogInformation("Removing the following resource IDs from the DB and storage.");
        foreach (var item in toRemove)
        {
            System.Console.WriteLine(item.Id);
        }
        _logger.LogInformation("Failed to remove.");
        foreach (var item in failedRevoke.Links)
        {
            System.Console.WriteLine(item);
        }

        await _resourceSave.RemoveList(toRemove);
        _resourceDb.Resources.RemoveRange(toRemove);
        await _resourceDb.SaveChangesAsync();

        return failedRevoke;
    }
    public async Task RemoveByLightroomAlbum(string albumUrl)
    {
        var items = await _resourceDb.Resources
        .Where(x => x.LightroomAlbum == albumUrl)
        .ToListAsync();

        await _resourceSave.RemoveList(items);

        _resourceDb.Resources.RemoveRange(items);
        await _resourceDb.SaveChangesAsync();
    }
    public async Task<List<string>?> GetLightroomOriginByRokuId(string rokuId)
    {
        return await _resourceDb.Resources
            .Where(x => x.RokuId == rokuId &&
                        x.Source == ImageShareSource.Lightroom)
            .Select(x => x.Origin)
            .ToListAsync();
    }
    public async Task RemoveByOrigin(List<string>? origins)
    {
        if (origins is null)
            return;

        var itemsToRemove = await _resourceDb.Resources
        .Where(x => origins.Contains(x.Origin))
        .ToListAsync();

        await _resourceSave.RemoveList(itemsToRemove);

        _resourceDb.Resources.RemoveRange(itemsToRemove);
        await _resourceDb.SaveChangesAsync();

        _logger.LogInformation($"Removed {itemsToRemove.Count} lightroom resources from resource database");
    }
    public async Task<Dictionary<Guid, string>> GetOldImgsForUpdatePackage(List<string>? imgsToKeep)
    {
        Dictionary<Guid, string> imgs = new();

        if (imgsToKeep is null)
            return imgs;

        List<ImageShare> toRemove = new();
        List<ImageShare> toAdd = new();

        foreach (var item in imgsToKeep)
        {
            string hash = GlobalHelpers.ComputeHashFromString(item);
            var resource = await _resourceDb.Resources
            .FirstOrDefaultAsync(r => r.Origin == hash);

            if (resource is null)
                continue;

            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var newKey = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var keyDerivation = _hmacService.Hash(newKey);

            var newResource = resource with
            {
                Key = keyDerivation,
                Id = Guid.NewGuid()
            };

            toRemove.Add(resource);
            toAdd.Add(newResource);

            imgs.Add(newResource.Id, newKey);
        }

        _logger.LogInformation("updated the following resource IDs and their keys in db.");
        foreach (var item in toAdd)
        {
            _logger.LogInformation(item.Id.ToString());
        }
        _logger.LogInformation("from old ids and their keys");
        foreach (var item in toRemove)
        {
            _logger.LogInformation(item.Id.ToString());
        }

        _resourceDb.Resources.RemoveRange(toRemove);
        await _resourceDb.Resources.AddRangeAsync(toAdd);
        await _resourceDb.SaveChangesAsync();

        return imgs;
    }
    public async Task WriteSessionImages(Guid linkSessionId, ImageShareSource source, List<IFormFile>? images = null, string lightroomAlbum = "")
    {
        if (images is null)
        {
            var linkSession = _linkSessions.GetSession<LinkSession>(linkSessionId);
            if (linkSession is null)
                throw new ArgumentNullException();

            var updatedSession = linkSession with { };

            using HttpClient client = new();

            if (!string.IsNullOrEmpty(linkSession.AccessToken))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", linkSession.AccessToken);
            }

            List<ImageShare> sharesToAdd = new();

            foreach (var item in linkSession.ImageServiceLinks)
            {
                var bytes = new byte[32];
                RandomNumberGenerator.Fill(bytes);
                var key = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                var keyDerivation = _hmacService.Hash(key);

                byte[] data = await client.GetByteArrayAsync(item);
                if (data.Length == 0)
                    continue;

                Guid shareId = Guid.NewGuid();

                ImageShare share = new()
                {
                    Id = shareId,
                    Key = keyDerivation,
                    KeyCreated = DateTime.UtcNow,
                    SessionCode = linkSession.SessionCode,
                    ImageUri = await _resourceSave.SaveResource(shareId, data, linkSession.MaxScreenSize, source),
                    CreatedOn = DateTime.UtcNow,
                    LightroomAlbum = lightroomAlbum,
                    RokuId = linkSession.RokuId,
                    Source = source,
                    Origin = GlobalHelpers.ComputeHashFromString(item)
                };
                updatedSession.ResourcePackage.Add(share.Id, key);
                sharesToAdd.Add(share);
            }
            await _resourceDb.AddRangeAsync(sharesToAdd);
            await _resourceDb.SaveChangesAsync();
            updatedSession.ReadyForTransfer = true;
            _linkSessions.SetSession<LinkSession>(linkSessionId, updatedSession);
        }
        else
        {
            var linkSession = _linkSessions.GetSession<LinkSession>(linkSessionId);
            if (linkSession is null)
                throw new ArgumentNullException();

            var updatedSession = linkSession with { };

            List<ImageShare> sharesToAdd = new();

            foreach (var item in images)
            {
                if (item.Length <= 0) continue;

                byte[] imgBytes;
                using (var ms = new MemoryStream())
                {
                    await item.CopyToAsync(ms);
                    imgBytes = ms.ToArray();
                }

                var bytes = new byte[32];
                RandomNumberGenerator.Fill(bytes);
                var key = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                var keyDerivation = _hmacService.Hash(key);

                Guid shareId = Guid.NewGuid();

                ImageShare share = new()
                {
                    Id = shareId,
                    Key = keyDerivation,
                    KeyCreated = DateTime.UtcNow,
                    SessionCode = linkSession.SessionCode,
                    ImageUri = await _resourceSave.SaveResource(shareId, imgBytes, linkSession.MaxScreenSize, source),
                    CreatedOn = DateTime.UtcNow,
                    RokuId = linkSession.RokuId,
                    Source = source
                };
                updatedSession.ResourcePackage.Add(share.Id, key);
                sharesToAdd.Add(share);
            }
            await _resourceDb.AddRangeAsync(sharesToAdd);
            await _resourceDb.SaveChangesAsync();
            updatedSession.ReadyForTransfer = true;
            _linkSessions.SetSession<LinkSession>(linkSessionId, updatedSession);
        }
    }
}