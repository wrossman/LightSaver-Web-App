using System.Text.Json.Serialization;

public class PickerSession
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("mediaItemsSet")]
    public bool MediaItemsSet { get; set; }

    [JsonPropertyName("pickerUri")]
    public string PickerUri { get; set; } = string.Empty;

    [JsonPropertyName("pollingConfig")]
    public PollingConfig PollingConfig { get; set; } = new PollingConfig();

    [JsonPropertyName("expireTime")]
    public DateTime ExpireTime { get; set; }
}

public class PollingConfig
{
    [JsonPropertyName("pollInterval")]
    public string PollInterval { get; set; } = string.Empty;

    [JsonPropertyName("timeoutIn")]
    public string TimeoutIn { get; set; } = string.Empty;
}