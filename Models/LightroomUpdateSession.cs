public record LightroomUpdateSession
{
    public string Id { get; set; } = "";
    public string RokuId { get; set; } = "";
    public bool ReadyForTransfer { get; set; } = false;
    public Dictionary<string, string> Links { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public bool Expired { get; set; } = false;
}