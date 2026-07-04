using System.Text.Json.Serialization;

namespace SayraClient.Models;

public class SecureMessageModel
{
    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}
