using System.Text.Json.Serialization;

namespace SayraClient.Models;

public class ExecutionResult
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "RESULT";

    [JsonPropertyName("action")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Message { get; set; } = string.Empty;

    public static ExecutionResult Success(string action, string message = "") => new()
    {
        Action = action,
        Status = "SUCCESS",
        Message = message
    };

    public static ExecutionResult Error(string action, string message) => new()
    {
        Action = action,
        Status = "ERROR",
        Message = message
    };
}
