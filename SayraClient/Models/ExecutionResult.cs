using System.Text.Json.Serialization;

namespace SayraClient.Models;

/// <summary>
/// Aligned with CommandResponse schema from openapi.yaml
/// </summary>
public class ExecutionResult
{
    [JsonPropertyName("commandId")]
    public string CommandId { get; set; } = string.Empty;

    [JsonPropertyName("pcId")]
    public string PcId { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // enum: [Pending, Sent, Executed, Failed]

    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ExecutionResult Success(string action, string result = "", string? pcId = null, string? commandId = null) => new()
    {
        Action = action,
        Status = "Executed",
        Result = result,
        PcId = pcId ?? string.Empty,
        CommandId = commandId ?? Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow
    };

    public static ExecutionResult Error(string action, string result, string? pcId = null, string? commandId = null) => new()
    {
        Action = action,
        Status = "Failed",
        Result = result,
        PcId = pcId ?? string.Empty,
        CommandId = commandId ?? Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow
    };
}
