public record RokuSession
{
    public string Id { get; init; } = "";
    public string RokuId { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string SourceAddress { get; init; } = "0.0.0.0";
    public string SessionCode { get; set; } = "";
    public bool ReadyForTransfer { get; set; } = false;
    public bool Expired { get; set; } = false;
    public int MaxScreenSize { get; set; } = 1920;

}