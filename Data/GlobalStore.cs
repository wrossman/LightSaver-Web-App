using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
public class GlobalStore
{
    private readonly ILogger<GlobalStore> _logger;
    private readonly GlobalImageStoreDbContext _resourceDb;
    private readonly IConfiguration _config;
    private readonly HmacHelper _hmacService;
    private readonly LinkSessions _linkSessions;
    public GlobalStore(LinkSessions linkSessions, GlobalImageStoreDbContext resourceDb, ILogger<GlobalStore> logger, IConfiguration config, HmacHelper hmacService)
    {
        _logger = logger;
        _resourceDb = resourceDb;
        _config = config;
        _hmacService = hmacService;
        _linkSessions = linkSessions;
    }
    public Dictionary<string, string>? GetResourcePackage(LinkSession LinkSession)
    {
        string sessionCode = LinkSession.SessionCode;
        string rokuId = LinkSession.RokuId;

        var links = _resourceDb.Resources
            .Where(img => img.SessionCode == sessionCode && img.RokuId == rokuId)
            .ToDictionary(img => img.Id.ToString(), img => img.Key);
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
    public async Task<(byte[] image, string fileType)> GetResourceData(string id, string key, string device)
    {
        var item = _resourceDb.Resources
        .Where(img => img.Id.ToString() == id && img.RokuId == device)
        .Select(img => img).SingleOrDefault();

        if (item is null || !_hmacService.Verify(key, item.Key))
        {
            throw new AuthenticationException();
        }

        byte[] img;
        try
        {
            img = await File.ReadAllBytesAsync(item.ImageUri);
        }
        catch
        {
            throw new IOException();
        }

        return (img, item.FileType);
    }
    // public Dictionary<Guid, string> GetUpdateKeys(string id, string key, string device)
    // {
    //     var item = _resourceDb.Resources
    //     .Where(img => img.Id.ToString() == id && img.RokuId == device)
    //     .Select(img => img).SingleOrDefault();

    //     if (item is null || !_hmacService.Verify(key, item.Key))
    //     {
    //         throw new AuthenticationException();
    //     }

    //     if (DateTime.UtcNow > item.KeyCreated.AddDays(20))

    //         return;
    // }
    public async Task<byte[]?> GetBackgroundData(string id, string key, string device, int height, int width)
    {
        (byte[] Image, string FileType) result;
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
            using var image = Image.Load(result.Image);
            image.Mutate(x =>
                        {
                            x.Resize(width / 8, height / 8);
                            x.GaussianBlur(6);
                            x.Resize(width, height);
                        });
            using var outputStream = new MemoryStream();
            image.Save(outputStream, new JpegEncoder());
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

        RemoveListFromLocalStore(sessions.Select(x => x.ImageUri).ToList());

        _resourceDb.Resources.RemoveRange(sessions);
        await _resourceDb.SaveChangesAsync();

        return true;
    }
    public void RemoveListFromLocalStore(List<string> uris)
    {
        foreach (var filePath in uris)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
    public void RemoveSingleFromLocalStore(string uri)
    {
        if (File.Exists(uri))
        {
            File.Delete(uri);
        }
    }
    public string WritePhotosToLocal(Guid resourceId, byte[] img, string? fileType, int maxScreenSize)
    {
        var resizedImg = ResizeToMaxBox(img, maxScreenSize);

        string folderPath = _config.GetValue<string>("LocalResourceStorePath")
         ?? "C:\\Users\\billuswillus\\Documents\\GitHub\\LightSaver-Web-App\\LocalResourceStore\\";
        var filePath = folderPath + resourceId;

        if (fileType is not null)
            filePath += "." + fileType;

        using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            fs.Write(resizedImg, 0, resizedImg.Length);
        }

        return filePath;
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
            .FirstOrDefaultAsync(x => x.Id == item.Key && x.RokuId == rokuId);

            if (session == null)
            {
                failedRevoke.Links.Add(item.Key, item.Value);
                continue;
            }

            if (!_hmacService.Verify(item.Value, session.Key))
            {
                _logger.LogWarning("An incorrect key was passed in a revoke resource package.");
                failedRevoke.Links.Add(item.Key, item.Value);
                continue;
            }

            RemoveSingleFromLocalStore(session.ImageUri);

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

        RemoveListFromLocalStore(items.Select(x => x.ImageUri).ToList());

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

        RemoveListFromLocalStore(itemsToRemove.Select(x => x.ImageUri).ToList());

        _resourceDb.Resources.RemoveRange(itemsToRemove);
        await _resourceDb.SaveChangesAsync();

        _logger.LogInformation($"Removed {itemsToRemove.Count} lightroom resources from resource database");
    }
    public byte[] ResizeToMaxBox(byte[] input, int maxScreenSize)
    {
        using var image = Image.Load(input);

        _logger.LogInformation($"Loaded image with height: {image.Height} and width {image.Width}");

        if (image.Width > maxScreenSize || image.Height > maxScreenSize)
        {
            _logger.LogInformation($"Resizing image with dimensions Width: {image.Width} Height: {image.Height} to max screen size of {maxScreenSize}");
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
    public async Task<Dictionary<Guid, string>> GetOldImgsForUpdatePackage(List<string>? imgsToKeep)
    {
        Dictionary<Guid, string> imgs = new();

        if (imgsToKeep is null)
            return imgs;

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

            _resourceDb.Resources.Remove(resource);
            await _resourceDb.Resources.AddAsync(newResource);

            await _resourceDb.SaveChangesAsync();

            imgs.Add(newResource.Id, newKey);
        }

        return imgs;
    }
    public async Task WriteSessionImages(Guid linkSessionId, ImageShareSource source, List<IFormFile>? images = null)
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

            foreach (KeyValuePair<string, string?> item in linkSession.ImageServiceLinks)
            {
                var bytes = new byte[32];
                RandomNumberGenerator.Fill(bytes);
                var key = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                var keyDerivation = _hmacService.Hash(key);

                byte[] data = await client.GetByteArrayAsync(item.Key);
                if (data.Length == 0)
                    continue;

                Guid shareId = Guid.NewGuid();

                ImageShare share = new()
                {
                    Id = shareId,
                    Key = keyDerivation,
                    KeyCreated = DateTime.UtcNow,
                    SessionCode = linkSession.SessionCode,
                    ImageUri = WritePhotosToLocal(shareId, data, item.Value, linkSession.MaxScreenSize),
                    CreatedOn = DateTime.UtcNow,
                    FileType = item.Value ?? "",
                    RokuId = linkSession.RokuId,
                    Source = source,
                    Origin = GlobalHelpers.ComputeHashFromString(item.Key)
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

                var finalImg = FixOrientation(imgBytes);

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
                    ImageUri = WritePhotosToLocal(shareId, finalImg, null, linkSession.MaxScreenSize),
                    CreatedOn = DateTime.UtcNow,
                    FileType = "", // should i figure out how to get the filetype? it isn't really necessary for roku
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
    public byte[] FixOrientation(byte[] imageBytes)
    {
        using var image = Image.Load(imageBytes);

        if (image.Metadata?.ExifProfile != null)
        {
            IExifValue<ushort>? orientation;
            if (image.Metadata.ExifProfile.TryGetValue(ExifTag.Orientation, out orientation))
            {
                object? orientationVal;
                if (orientation is not null)
                    orientationVal = orientation.GetValue();
                else
                    return imageBytes;

                UInt16 orientationShort;
                if (orientationVal is not null)
                    orientationShort = (UInt16)orientationVal;
                else
                    return imageBytes;

                switch (orientationShort)
                {
                    case 2: image.Mutate(x => x.Flip(FlipMode.Horizontal)); break;
                    case 3: image.Mutate(x => x.Rotate(180)); break;
                    case 4: image.Mutate(x => x.Flip(FlipMode.Vertical)); break;
                    case 5: image.Mutate(x => { x.Rotate(90); x.Flip(FlipMode.Horizontal); }); break;
                    case 6: image.Mutate(x => x.Rotate(90)); break;
                    case 7: image.Mutate(x => { x.Rotate(-90); x.Flip(FlipMode.Horizontal); }); break;
                    case 8: image.Mutate(x => x.Rotate(-90)); break;
                }

                // After fixing, remove orientation tag to avoid double-rotation later
                image.Metadata.ExifProfile.RemoveValue(ExifTag.Orientation);
            }

        }

        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }
}