using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
public class UploadImages
{
    private readonly ILogger<LinkSessions> _logger;
    private readonly GlobalStoreHelpers _store;
    private readonly LinkSessions _linkSessions;
    private readonly HmacHelper _hmacService;
    public UploadImages(ILogger<LinkSessions> logger, GlobalStoreHelpers store, LinkSessions linkSessions, HmacHelper hmacService)
    {
        _logger = logger;
        _store = store;
        _hmacService = hmacService;
        _linkSessions = linkSessions;
    }
    public async Task<bool> UploadImageFlow(List<IFormFile> images, Guid sessionId)
    {
        var session = _linkSessions.GetSession<LinkSession>(sessionId);
        if (session is null)
            return false;

        var updatedSession = session with { };

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

            ImageShare share = new()
            {
                Id = Guid.NewGuid(),
                Key = keyDerivation,
                KeyCreated = DateTime.UtcNow,
                SessionCode = session.SessionCode,
                ImageStream = finalImg,
                CreatedOn = DateTime.UtcNow,
                FileType = "", // should i figure out how to get the filetype? it isn't really necessary for roku
                RokuId = session.RokuId,
                Source = ImageShareSource.Upload
            };
            await _store.WriteResourceToStore(share, session.MaxScreenSize);
            updatedSession.ResourcePackage.Add(share.Id, key);
        }

        _linkSessions.SetSession<LinkSession>(sessionId, updatedSession);

        if (_linkSessions.SetReadyToTransfer(sessionId))
            return true;
        else
            return false;
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