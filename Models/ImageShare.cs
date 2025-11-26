public record ImageShare
{
    public string Id { get; init; } = "";
    public string Key { get; init; } = "";
    public string SessionCode { get; set; } = "";
    public byte[] ImageStream { get; set; } = new byte[0];
    public DateTime CreatedOn { get; init; }
    public string FileType { get; init; } = "";
    public string RokuId { get; set; } = "";
    public string Source { get; set; } = "";
    public string OriginUrl { get; set; } = "";
    public string LightroomAlbum { get; set; } = "";
}