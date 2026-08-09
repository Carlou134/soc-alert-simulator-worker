using System.Text.Json.Serialization;

namespace SplunkSocWorker.Models;

public class HecEvent
{
    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("index")]
    public string Index { get; set; } = string.Empty;

    [JsonPropertyName("sourcetype")]
    public string Sourcetype { get; set; } = string.Empty;

    [JsonPropertyName("event")]
    public AlertRecord Event { get; set; } = default!;
}
