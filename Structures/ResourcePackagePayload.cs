using System.Text.Json.Serialization;

public record ResourcePackageJson
{
    [JsonPropertyName("Links")]
    public List<string> Links { get; init; } = new();

    [JsonPropertyName("Key")]
    public string Key { get; init; } = string.Empty;
}
