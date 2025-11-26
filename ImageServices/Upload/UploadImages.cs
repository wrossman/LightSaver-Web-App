using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
public class UploadImages
{
    private readonly ILogger<UserSessions> _logger;
    private readonly GlobalStoreHelpers _store;
    private readonly SessionHelpers _sessions;
    public UploadImages(ILogger<UserSessions> logger, GlobalStoreHelpers store, SessionHelpers sessions)
    {
        _logger = logger;
        _store = store;
        _sessions = sessions;
    }
    public async Task<bool> UploadImageFlow(List<IFormFile> images, UserSession userSession)
    {
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
                SessionCode = userSession.SessionCode,
                ImageStream = finalImg,
                CreatedOn = DateTime.UtcNow,
                FileType = "", // should i figure out how to get the filetype? it isnt really necessary for roku
                RokuId = userSession.RokuId,
                Source = "upload"
            };
            await _store.WriteResourceToStore(share, userSession.MaxScreenSize);
        }

        UserSessions.CodesReadyForTransfer.Enqueue(userSession.SessionCode);
        return true;
    }
    public async Task ExpireCreds(string sessionCode)
    {
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