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
    public async Task<string> SaveResource(Guid resourceId, byte[] img, int screenWidth, int screenHeight, ImageShareSource source)
    {
        _logger.LogInformation("Writing resource to local directory.");
        var processedImg = _imageProcessors.ProcessImage(img, screenWidth, screenHeight);

        // create a separate array for the background image
        var backgroundArray = new byte[processedImg.Length];
        processedImg.CopyTo(backgroundArray);
        var processedBackground = _imageProcessors.ProcessBackground(backgroundArray, screenWidth, screenHeight);

        string? folderPath = _config.GetValue<string>("LocalResourceStorePath");
        if (folderPath is null)
            throw new ArgumentNullException("Local Write Path");

        var imgFilePath = folderPath + resourceId;

        _logger.LogInformation("Writing to " + imgFilePath);

        using (FileStream fs = new FileStream(imgFilePath, FileMode.Create, FileAccess.Write))
        {
            await fs.WriteAsync(processedImg, 0, processedImg.Length);
        }

        // write the background image if we are able to successfully process it, ad bg postfix
        if (processedBackground is not null)
        {
            var backgroundFilePath = folderPath + resourceId + "_bg";

            using (FileStream fs = new FileStream(backgroundFilePath, FileMode.Create, FileAccess.Write))
            {
                await fs.WriteAsync(processedBackground, 0, processedBackground.Length);
            }
        }

        return imgFilePath;
    }
    public async Task<bool> RemoveSingle(ImageShare resource)
    {
        if (File.Exists(resource.Id.ToString()))
            File.Delete(resource.Id.ToString());

        if (File.Exists(resource.Id + "_bg"))
            File.Delete(resource.Id + "_bg");

        return true;
    }
    public async Task<bool> RemoveList(List<ImageShare> resources)
    {
        string? folderPath = _config.GetValue<string>("LocalResourceStorePath");
        if (folderPath is null)
            throw new ArgumentNullException("Local Write Path");

        foreach (var resource in resources)
        {
            if (File.Exists(folderPath + resource.Id.ToString()))
                File.Delete(folderPath + resource.Id.ToString());

            if (File.Exists(folderPath + resource.Id + "_bg"))
                File.Delete(folderPath + resource.Id + "_bg");

        }
        return true;
    }

    public async Task<byte[]> GetResource(ImageShare image, bool background)
    {
        byte[] img;

        if (background)
            img = await File.ReadAllBytesAsync(image.ImageUri + "_bg");
        else
            img = await File.ReadAllBytesAsync(image.ImageUri);

        return img;
    }

    public async Task<List<string?>> GetResources()
    {
        string? folderPath = _config.GetValue<string>("LocalResourceStorePath");
        if (folderPath is null)
            throw new ArgumentNullException("Local Write Path");

        return Directory
            .EnumerateFiles(folderPath)
            .Select(Path.GetFileName)
            .ToList();
    }
}