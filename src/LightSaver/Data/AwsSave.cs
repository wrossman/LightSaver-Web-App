using Amazon.S3;
using Amazon.S3.Model;
public class AwsSave : IResourceSave
{
    private readonly ImageProcessors _imageProcessors;
    private readonly ILogger<AwsSave> _logger;
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    public AwsSave(IAmazonS3 client, ILogger<AwsSave> logger, IConfiguration config, ImageProcessors imageProcessors)
    {
        _client = client;
        _imageProcessors = imageProcessors;
        _logger = logger;

        string? bucket = config["AwsBucketName"];
        if (bucket is null)
        {
            throw new InvalidOperationException("Failed to get aws s3 bucket name.");
        }
        _bucket = bucket;
    }
    public async Task<byte[]> GetResource(ImageShare resource)
    {
        // Create a GetObject request
        var request = new GetObjectRequest
        {
            BucketName = _bucket,
            Key = resource.Id.ToString(),
        };

        // Issue request and remember to dispose of the response
        using GetObjectResponse response = await _client.GetObjectAsync(request);
        using var stream = response.ResponseStream;

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        return ms.ToArray();
    }

    public async Task<bool> RemoveList(List<ImageShare> uris)
    {
        IEnumerable<string> imagePaths = uris.Select(x => x.ImageUri);

        var request = new DeleteObjectsRequest
        {
            BucketName = _bucket,
            Objects = imagePaths
        .Select(k => new KeyVersion { Key = k })
        .ToList(),
            Quiet = true
        };

        var response = await _client.DeleteObjectsAsync(request);

        if (response.DeleteErrors.Count > 0)
        {
            _logger.LogError("Failed to remove all images from aws bucket");
            foreach (var error in response.DeleteErrors)
            {
                _logger.LogError(
                    "Failed to delete S3 object {Key}: {Code} - {Message}",
                    error.Key,
                    error.Code,
                    error.Message);
            }
            return false;
        }

        return true;
    }

    public async Task<bool> RemoveSingle(ImageShare resource)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucket,
            Key = resource.ImageUri
        };

        await _client.DeleteObjectAsync(request);
        return true;
    }

    public async Task<string> SaveResource(Guid resourceId, byte[] img, int maxScreenSize, ImageShareSource source)
    {
        _logger.LogInformation($"Writing resource to {_bucket} in aws.");

        var processedImg = _imageProcessors.ProcessImage(img, maxScreenSize, source);

        using var stream = new MemoryStream(processedImg);

        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = resourceId.ToString(),
                InputStream = stream,
            };

            await _client.PutObjectAsync(request);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning($"Could not upload {resourceId} to {_bucket}: '{ex.Message}'");
            return "";
        }
        return resourceId.ToString();
    }
}