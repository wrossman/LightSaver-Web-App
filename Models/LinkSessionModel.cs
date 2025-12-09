public record LinkSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; }
    public string AccessToken { get; init; } = "";
    public string RokuId { get; init; } = "";
    public string SourceAddress { get; init; } = "0.0.0.0";
    public string SessionCode { get; init; } = "";
    public bool ReadyForTransfer { get; set; } = false;
    public bool Expired { get; init; } = false;
    public int MaxScreenSize { get; init; } = 1920;
    public Dictionary<string, string?> ImageServiceLinks { get; set; } = new();
    public Dictionary<Guid, string> ResourcePackage { get; init; } = new();

}