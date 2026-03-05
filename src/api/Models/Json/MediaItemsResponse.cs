using System.Text.Json.Serialization;

public class MediaItemsResponse
{
    [JsonPropertyName("mediaItems")]
    public List<MediaItem> MediaItems { get; set; } = new();
}

public class MediaItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("createTime")]
    public DateTime CreateTime { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("mediaFile")]
    public MediaFile MediaFile { get; set; } = new MediaFile();
}

public class MediaFile
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "";

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "";

    [JsonPropertyName("mediaFileMetadata")]
    public MediaFileMetadata MediaFileMetadata { get; set; } = new MediaFileMetadata();

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";
}

public class MediaFileMetadata
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("cameraMake")]
    public string CameraMake { get; set; } = "";

    [JsonPropertyName("cameraModel")]
    public string CameraModel { get; set; } = "";

    [JsonPropertyName("photoMetadata")]
    public PhotoMetadata PhotoMetadata { get; set; } = new PhotoMetadata();
}

public class PhotoMetadata
{
    [JsonPropertyName("focalLength")]
    public double FocalLength { get; set; }

    [JsonPropertyName("apertureFNumber")]
    public double ApertureFNumber { get; set; }

    [JsonPropertyName("isoEquivalent")]
    public int IsoEquivalent { get; set; }

    [JsonPropertyName("exposureTime")]
    public string ExposureTime { get; set; } = "";
}