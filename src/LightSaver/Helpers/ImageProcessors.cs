using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Formats.Webp;
public class ImageProcessors
{
    private readonly ILogger<ImageProcessors> _logger;
    public ImageProcessors(ILogger<ImageProcessors> logger)
    {
        _logger = logger;
    }
    public byte[] ProcessImage(byte[] input, int maxScreenSize, ImageShareSource source)
    {
        using var image = Image.Load(input);

        _logger.LogInformation($"Loaded image with format {image.Metadata.DecodedImageFormat?.Name ?? "Unknown"} height: {image.Height} and width {image.Width}");

        FixOrientation(image);
        ResizeToMaxBox(image, maxScreenSize);

        using var outputStream = new MemoryStream();
        image.Save(outputStream, new WebpEncoder());

        return outputStream.ToArray();
    }
    public Image ResizeToMaxBox(Image image, int maxScreenSize)
    {
        if (image.Width > maxScreenSize || image.Height > maxScreenSize)
        {
            var format = image.Metadata.DecodedImageFormat;

            _logger.LogInformation($"Resizing image with dimensions Width: {image.Width} Height: {image.Height} to max screen size of {maxScreenSize}");
            var options = new ResizeOptions
            {
                Mode = ResizeMode.Max, // maintain aspect ratio
                Size = new Size(maxScreenSize, maxScreenSize)
            };

            image.Mutate(x => x.Resize(options));

            _logger.LogInformation($"New dimensions: Width: {image.Width} Height: {image.Height}");

            return image;
        }
        else
        {
            return image;
        }
    }
    public Image FixOrientation(Image image)
    {
        IExifValue<ushort>? orientation;
        if (image.Metadata.ExifProfile == null || !image.Metadata.ExifProfile.TryGetValue(ExifTag.Orientation, out orientation))
        {
            return image;
        }

        object? orientationVal;
        if (orientation is not null)
            orientationVal = orientation.GetValue();
        else
            return image;

        UInt16 orientationShort;
        if (orientationVal is not null)
            orientationShort = (UInt16)orientationVal;
        else
            return image;

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
        return image;
    }
}