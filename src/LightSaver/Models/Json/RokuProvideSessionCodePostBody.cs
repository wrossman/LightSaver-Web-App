public record RokuProvideSessionCodePostBody
{
    public string RokuId { get; set; } = "";
    public int ScreenWidth { get; set; } = 1920;
    public int ScreenHeight { get; set; } = 1080;
}