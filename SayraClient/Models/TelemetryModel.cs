using System.Text.Json.Serialization;

namespace SayraClient.Models;

/// <summary>
/// Aligned with TelemetryResponse schema from openapi.yaml
/// </summary>
public class TelemetryModel
{
    [JsonPropertyName("cpu")]
    public double Cpu { get; set; }

    [JsonPropertyName("ram")]
    public double Ram { get; set; }

    [JsonPropertyName("uptime")]
    public int Uptime { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
