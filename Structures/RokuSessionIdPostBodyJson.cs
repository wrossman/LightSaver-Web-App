public record RokuSessionIdPostBody
{
    public string RokuId { get; init; } = "";
    public string RokuSessionCode { get; init; } = "";
}