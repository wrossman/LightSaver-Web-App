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
    public async Task<string> SaveResource(Guid resourceId, byte[] img, string? fileType, int maxScreenSize)
    {
        _logger.LogInformation("Writing resource to local directory.");
        var resizedImg = _imageProcessors.ResizeToMaxBox(img, maxScreenSize);

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