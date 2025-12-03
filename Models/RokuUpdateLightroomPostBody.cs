public record RokuUpdateLightroomPostBody
{
    public Guid Id { get; init; } = Guid.Empty;
    public string Key { get; init; } = "";
    public string RokuId { get; init; } = "";
}