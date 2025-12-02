public record ImageShare
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Key { get; init; } = "";
    public string SessionCode { get; set; } = "";
    public byte[] ImageStream { get; set; } = new byte[0];
    public DateTime CreatedOn { get; init; }
    public string FileType { get; init; } = "";
    public string RokuId { get; set; } = "";
    public string Source { get; set; } = "";
    public string Origin { get; set; } = "";
    public string LightroomAlbum { get; set; } = "";
}