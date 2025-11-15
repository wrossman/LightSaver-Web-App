public record ImageShare
{
    public ImageShare(string hash, string key, string sessionCode, byte[] imageStream, DateTime createdOn, string fileType, string rokuId)
    {
        Id = hash;
        Key = key;
        SessionCode = sessionCode;
        ImageStream = imageStream;
        CreatedOn = createdOn;
        FileType = fileType;
        RokuId = rokuId;
    }
    public string Id { get; init; }
    public string Key { get; init; }
    public string SessionCode { get; init; }
    public byte[] ImageStream { get; init; }
    public DateTime CreatedOn { get; init; }
    public string FileType { get; init; }
    public string RokuId { get; set; }
}