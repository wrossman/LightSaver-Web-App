using Azure.Storage.Blobs;
public class CloudSave : IResourceSave
{
    private readonly BlobServiceClient _blobService;
    private readonly string _containerName;
    private readonly ImageProcessors _imageProcessors;
    private readonly ILogger<CloudSave> _logger;
    public CloudSave(BlobServiceClient blobService, ILogger<CloudSave> logger, IConfiguration config, ImageProcessors imageProcessors)
    {
        _imageProcessors = imageProcessors;
        _blobService = blobService;
        _containerName = config["AzureStorage:ContainerName"] ?? "resources";
        _logger = logger;
    }
    public async Task<byte[]> GetResource(ImageShare resource)
    {
        var container = _blobService.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(resource.Id.ToString());
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
            await blob.DeleteIfExistsAsync();
        }
        return true;
    }

    public async Task<bool> RemoveSingle(ImageShare resource)
    {
        var container = _blobService.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(resource.Id.ToString());
        return await blob.DeleteIfExistsAsync();
    }

    public async Task<string> SaveResource(Guid resourceId, byte[] img, string? fileType, int maxScreenSize)
    {
        _logger.LogInformation("Writing resource to Azure resources container.");
        var resizedImg = _imageProcessors.ResizeToMaxBox(img, maxScreenSize);

        BlobClient blob;
        try
        {
            using var imageStream = new MemoryStream(resizedImg);

            var container = _blobService.GetBlobContainerClient(_containerName);
            blob = container.GetBlobClient(resourceId.ToString());
            await blob.UploadAsync(imageStream);
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to write to Azure resources container." + e.Message);
            throw new IOException();
        }

        return blob.Uri.ToString();
    }
}