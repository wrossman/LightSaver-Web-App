public record RokuProvideSessionCodePostBody
{
    public string RokuId { get; set; } = "";
    public int MaxScreenSize { get; set; } = 1920;
}