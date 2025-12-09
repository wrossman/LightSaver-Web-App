public record ImageShare
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public required DateTime KeyCreated { get; set; }
    public string SessionCode { get; set; } = "";
    public string ImageUri { get; set; } = "";
    public DateTime CreatedOn { get; init; }
    public string FileType { get; init; } = "";
    public string RokuId { get; set; } = "";
    public ImageShareSource Source { get; set; } = ImageShareSource.Unknown;
    public string Origin { get; set; } = "";
    public string LightroomAlbum { get; set; } = "";
}