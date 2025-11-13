public record ImageShare
{
    public ImageShare(string hash, string sessionCode, byte[] imageStream, DateTime createdOn, string fileType)
    {
        Hash = hash;
        SessionCode = sessionCode;
        ImageStream = imageStream;
        CreatedOn = createdOn;
        FileType = fileType;
    }
    public string Hash { get; init; }
    public string SessionCode { get; init; }
    public byte[] ImageStream { get; init; }
    public DateTime CreatedOn { get; init; }

    public string FileType { get; init; }
}