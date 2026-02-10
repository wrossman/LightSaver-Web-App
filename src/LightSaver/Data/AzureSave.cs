using Azure.Storage.Blobs;
public class AzureSave : IResourceSave
{
    private readonly BlobServiceClient _blobService;
    private readonly string _containerName;
    private readonly ImageProcessors _imageProcessors;
    private readonly ILogger<AzureSave> _logger;
    public AzureSave(BlobServiceClient blobService, ILogger<AzureSave> logger, IConfiguration config, ImageProcessors imageProcessors)
    {
        _imageProcessors = imageProcessors;
        _blobService = blobService;
        _containerName = config["AzureStorage:ContainerName"] ?? "resources";
        _logger = logger;
    }
    public async Task<byte[]> GetResource(ImageShare resource, bool background)
    {
        var container = _blobService.GetBlobContainerClient(_containerName);

        BlobClient blob;
        if (background)
            blob = container.GetBlobClient(resource.Id.ToString() + "_bg");
        else
            blob = container.GetBlobClient(resource.Id.ToString());

        var content = await blob.DownloadContentAsync();
        byte[] img = content.Value.Content.ToArray();
        return img;
    }
    public async Task<bool> RemoveList(List<ImageShare> resources)
    {
        foreach (var resource in resources)
        {
            var container = _blobService.GetBlobContainerClient(_containerName);
            var blob = container.GetBlobClient(resource.Id.ToString());
            var backgroundBlob = container.GetBlobClient(resource.Id.ToString() + "_bg");
            await blob.DeleteIfExistsAsync();
            await backgroundBlob.DeleteIfExistsAsync();
        }
        return true;
    }

    public async Task<bool> RemoveSingle(ImageShare resource)
    {
        var container = _blobService.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(resource.Id.ToString());
        var backgroundBlob = container.GetBlobClient(resource.Id.ToString() + "_bg");
        return await blob.DeleteIfExistsAsync() && await backgroundBlob.DeleteIfExistsAsync();
    }

    public async Task<string> SaveResource(Guid resourceId, byte[] img, int screenWidth, int screenHeight, ImageShareSource source)
    {
        _logger.LogInformation("Writing resource to Azure resources container.");
        var processedImg = _imageProcessors.ProcessImage(img, screenWidth, screenHeight);

        // create a separate array for the background image
        var backgroundArray = new byte[processedImg.Length];
        processedImg.CopyTo(backgroundArray);
        var processedBackground = _imageProcessors.ProcessBackground(backgroundArray, screenWidth, screenHeight);

        BlobClient blob;
        BlobClient backgroundBlob;
        try
        {
            using var imageStream = new MemoryStream(processedImg);

            var container = _blobService.GetBlobContainerClient(_containerName);
            blob = container.GetBlobClient(resourceId.ToString());
            await blob.UploadAsync(imageStream);

            if (processedBackground is not null)
            {
                using var backgroundStream = new MemoryStream(processedBackground);
                backgroundBlob = container.GetBlobClient(resourceId.ToString() + "_bg");
                await backgroundBlob.UploadAsync(backgroundStream);
            }
            else
            {
                _logger.LogWarning("Failed to write background image to azure.");
            }

        }
        catch (Exception e)
        {
            _logger.LogError("Failed to write to Azure resources container." + e.Message);
            throw new IOException();
        }

        return blob.Uri.ToString();
    }
}