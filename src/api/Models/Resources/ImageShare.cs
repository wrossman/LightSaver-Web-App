public record ImageShare
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public DateTime KeyCreated { get; set; } = DateTime.UtcNow;
    public string SessionCode { get; set; } = "";
    public string ImageUri { get; set; } = "";
    public DateTime CreatedOn { get; init; } = DateTime.UtcNow;
    public string RokuId { get; set; } = "";
    public ImageShareSource Source { get; set; } = ImageShareSource.Unknown;
    public string Origin { get; set; } = "";
    public string LightroomAlbum { get; set; } = "";
}