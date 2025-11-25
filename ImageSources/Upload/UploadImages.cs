using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
public class UploadImages
{
    private readonly ILogger<UserSessions> _logger;
    private readonly RokuSessionDbContext _rokuSessionDb;
    private readonly UserSessionDbContext _userSessionDb;
    private readonly GlobalImageStoreDbContext _resourceDbContext;
    private readonly GlobalStoreHelpers _store;
    private readonly SessionHelpers _sessions;
    public UploadImages(ILogger<UserSessions> logger, UserSessionDbContext userSessionDb, RokuSessionDbContext rokuSessionDb, GlobalImageStoreDbContext resourceDbContext, GlobalStoreHelpers store, SessionHelpers sessions)
    {
        _logger = logger;
        _userSessionDb = userSessionDb;
        _rokuSessionDb = rokuSessionDb;
        _resourceDbContext = resourceDbContext;
        _store = store;
        _sessions = sessions;
    }
    public async Task<bool> UploadImageFlow(List<IFormFile> images, string sessionId)
    {
        var sessionCode = await _userSessionDb.Sessions
                            .Where(s => s.Id == sessionId)
                            .Select(s => s.SessionCode)
                            .FirstOrDefaultAsync();
        if (sessionCode is null)
        {
            _logger.LogWarning("Failed to locate user session with sessionId " + sessionId);
            return false;
        }
        var rokuId = await _rokuSessionDb.Sessions
                        .Where(s => s.SessionCode == sessionCode)
                        .Select(s => s.RokuId)
                        .FirstOrDefaultAsync();
        if (rokuId is null)
        {
            _logger.LogWarning("Failed to locate roku session with session code " + sessionCode);
            return false;
        }

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

            string hash = GlobalHelpers.ComputeHashFromBytes(finalImg);
            hash = hash + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

            ImageShare share = new()
            {
                Id = hash,
                Key = key,
                SessionCode = sessionCode,
                ImageStream = finalImg,
                CreatedOn = DateTime.UtcNow,
                FileType = "", // should i figure out how to get the filetype? it isnt really necessary for roku
                RokuId = rokuId,
                Source = "upload"
            };
            _resourceDbContext.Resources.Add(share);
            await _resourceDbContext.SaveChangesAsync();
        }

        UserSessions.CodesReadyForTransfer.Enqueue(sessionCode);
        return true;
    }
    public async Task ExpireCreds(string sessionCode)
    {
        //remove sessioncode reference from resources
        if (await _store.ScrubSessionCode(sessionCode))
            _logger.LogInformation($"Scrubbed Image Resources of session code {sessionCode}");
        else
            _logger.LogWarning($"Failed to scrub resources of session code {sessionCode}");

        // expire user and roku session associated with session code
        if (await _sessions.ExpireRokuSession(sessionCode))
            _logger.LogInformation("Set roku session for expiration due to resource package delivery.");
        else
            _logger.LogWarning("Failed to set expire for roku session after resource package delivery.");

        if (await _sessions.ExpireUserSession(sessionCode))
            _logger.LogInformation("Set user session for expiration due to resource package delivery.");
        else
            _logger.LogWarning("Failed to set expire for user session after resource package delivery.");

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

                _logger.LogInformation("Rotating uploaded image. Orientation number: " + orientationShort);
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