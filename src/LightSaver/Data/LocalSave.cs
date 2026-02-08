public class LocalSave : IResourceSave
{
    private readonly ImageProcessors _imageProcessors;
    private readonly IConfiguration _config;
    private readonly ILogger<LocalSave> _logger;
    public LocalSave(ImageProcessors imageProcessors, IConfiguration config, ILogger<LocalSave> logger)
    {
        _config = config;
        _logger = logger;
        _imageProcessors = imageProcessors;
    }
    public async Task<string> SaveResource(Guid resourceId, byte[] img, int maxScreenSize, ImageShareSource source)
    {
        _logger.LogInformation("Writing resource to local directory.");
        var processedImg = _imageProcessors.ProcessImage(img, maxScreenSize, source);

        string? folderPath = _config.GetValue<string>("LocalResourceStorePath");
        if (folderPath is null)
            throw new ArgumentNullException("Local Write Path");

        var filePath = folderPath + resourceId;

        _logger.LogInformation("Writing to " + filePath);

        using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            await fs.WriteAsync(processedImg, 0, processedImg.Length);
        }

        return filePath;
    }
    public async Task<bool> RemoveSingle(ImageShare resource)
    {
        if (File.Exists(resource.ImageUri))
        {
            File.Delete(resource.ImageUri);
        }
        return true;
    }
    public async Task<bool> RemoveList(List<ImageShare> resources)
    {
        foreach (var resource in resources)
        {
            if (File.Exists(resource.ImageUri))
            {
                File.Delete(resource.ImageUri);
            }
        }
        return true;
    }

    public async Task<byte[]> GetResource(ImageShare image)
    {
        byte[] img;
        try
        {
            img = await File.ReadAllBytesAsync(image.ImageUri);
        }
        catch
        {
            throw new IOException();
        }
        return img;
    }
}