using System.Text.Json.Serialization;

namespace SayraClient.Models;

/// <summary>
/// Aligned with SendCommandRequest schema from openapi.yaml
/// </summary>
public class CommandModel
{
    /// <summary>
    /// Message type (COMMAND, PING, etc.).
    /// May be inferred from presence of 'action' if missing in JSON.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("pcId")]
    public string PcId { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }
}
