public record ImageShare
{
    public string Id { get; init; } = "";
    public string Key { get; init; } = "";
    public string SessionCode { get; init; } = "";
    public byte[] ImageStream { get; init; } = new byte[0];
    public DateTime CreatedOn { get; init; }
    public string FileType { get; init; } = "";
    public string RokuId { get; set; } = "";
}