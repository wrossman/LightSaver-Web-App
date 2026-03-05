public record ResourceRequest
{
    public Guid Id { get; init; } = Guid.Empty;
    public string Key { get; init; } = "";
    public string RokuId { get; init; } = "";
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public ResourceRequest(Guid id, string key, string rokuId, int screenWidth, int screenHeight)
    {
        Id = id;
        Key = key;
        RokuId = rokuId;
        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;
    }
}