using System.Text.Json.Serialization;

public class PickerSession
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("mediaItemsSet")]
    public bool MediaItemsSet { get; set; }

    [JsonPropertyName("pickerUri")]
    public string PickerUri { get; set; }

    [JsonPropertyName("pollingConfig")]
    public PollingConfig PollingConfig { get; set; }

    [JsonPropertyName("expireTime")]
    public DateTime ExpireTime { get; set; }
}

public class PollingConfig
{
    [JsonPropertyName("pollInterval")]
    public string PollInterval { get; set; }

    [JsonPropertyName("timeoutIn")]
    public string TimeoutIn { get; set; }
}