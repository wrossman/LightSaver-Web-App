public class UploadStatusResponse
{
    public UploadStatusResponse(int uploadedImages, int totalImages, string status)
    {
        UploadedImages = uploadedImages;
        TotalImages = totalImages;
        Status = status;
    }
    public int UploadedImages { get; set; }
    public int TotalImages { get; set; }
    public string Status { get; set; } = string.Empty;
}