using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
public class ImageProcessors
{
    private readonly ILogger<ImageProcessors> _logger;
    public ImageProcessors(ILogger<ImageProcessors> logger)
    {
        _logger = logger;
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