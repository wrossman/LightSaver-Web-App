public record RokuReceptionPostBody
{
    public Guid SessionId { get; init; } = Guid.Empty;
    public string RokuId { get; init; } = "";
    public string SessionCode { get; init; } = "";
}