public record ResourceRequest
{
    public string Location { get; init; } = "";
    public string Key { get; init; } = "";
    public string RokuId { get; init; } = "";
    public int MaxScreenSize { get; set; }
    public ResourceRequest(string location, string key, string rokuId)
    {
        Location = location;
        Key = key;
        RokuId = rokuId;
    }
}