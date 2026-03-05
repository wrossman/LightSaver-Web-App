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
    public byte[] ProcessImage(byte[] input, int screenWidth, int screenHeight)
    {
        using var image = Image.Load(input);

        _logger.LogInformation($"Loaded image with format {image.Metadata.DecodedImageFormat?.Name ?? "Unknown"} height: {image.Height} and width {image.Width}");

        FixOrientation(image);
        ResizeToScreen(image, screenWidth, screenHeight);

        using var outputStream = new MemoryStream();
        image.Save(outputStream, new WebpEncoder());

        return outputStream.ToArray();
    }
    public byte[]? ProcessBackground(byte[] input, int screenWidth, int screenHeight)
    {
        try
        {
            using var image = Image.Load(input);
            image.Mutate(x =>
                        {
                            x.Resize(image.Width / 4, image.Height / 4);
                            x.GaussianBlur(7);
                            x.Resize(screenWidth, screenHeight);
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
    public Image ResizeToScreen(Image image, int screenWidth, int screenHeight)
    {
        // thanks chat
        if (image == null)
            throw new ArgumentNullException(nameof(image));

        if (screenWidth <= 0 || screenHeight <= 0)
            throw new ArgumentOutOfRangeException("Screen dimensions must be positive.");

        // Only resize if it exceeds the target bounds.
        if (image.Width <= screenWidth && image.Height <= screenHeight)
            return image;

        _logger.LogInformation(
            "Resizing image from {Width}x{Height} to fit within {ScreenWidth}x{ScreenHeight}",
            image.Width,
            image.Height,
            screenWidth,
            screenHeight
        );

        var options = new ResizeOptions
        {
            Mode = ResizeMode.Max, // preserve aspect ratio, fit within bounds
            Size = new Size(screenWidth, screenHeight)
        };

        image.Mutate(ctx => ctx.Resize(options));

        _logger.LogInformation(
            "Resized image dimensions: {Width}x{Height}",
            image.Width,
            image.Height
        );

        return image;
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