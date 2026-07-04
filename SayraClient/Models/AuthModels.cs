using System.Text.Json.Serialization;

namespace SayraClient.Models;

public class AuthChallengeModel
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "AUTH_CHALLENGE";

    [JsonPropertyName("challenge")]
    public string Challenge { get; set; } = string.Empty;
}

public class AuthResponseModel
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "AUTH_RESPONSE";

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("session_key")]
    public string? EncryptedSessionKey { get; set; }
}

public class AuthStatusModel
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "AUTH_STATUS";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // "SUCCESS" or "FAILED"

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
