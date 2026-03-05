public record LightroomUpdateSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Key { get; init; } = "";
    public string RokuId { get; init; } = "";
    public bool ReadyForTransfer { get; init; } = false;
    public Dictionary<Guid, string> ResourcePackage { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public bool Expired { get; init; } = false;
}