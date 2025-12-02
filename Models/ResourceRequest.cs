public record ResourceRequest
{
    public Guid Id { get; init; } = Guid.Empty;
    public string Key { get; init; } = "";
    public string RokuId { get; init; } = "";
    public int MaxScreenSize { get; set; }
    public ResourceRequest(Guid id, string key, string rokuId, int maxScreenSize)
    {
        Id = id;
        Key = key;
        RokuId = rokuId;
        MaxScreenSize = maxScreenSize;
    }
}