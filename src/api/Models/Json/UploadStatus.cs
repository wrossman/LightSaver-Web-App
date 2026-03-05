public class UploadStatusModel
{
    public UploadStatusModel(int downloadedImages, int totalImages, string status)
    {
        DownloadedImages = downloadedImages;
        TotalImages = totalImages;
        Status = status;
    }
    public int DownloadedImages { get; set; }
    public int TotalImages { get; set; }
    public string Status { get; set; } = string.Empty;
}