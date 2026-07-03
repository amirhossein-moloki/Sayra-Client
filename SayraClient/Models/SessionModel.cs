using System.Text.Json.Serialization;

namespace SayraClient.Models;

public enum SessionStatus
{
    IDLE,
    ACTIVE,
    PAUSED,
    ENDED
}

public class SessionModel
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("pcId")]
    public string PcId { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; } // in minutes

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SessionStatus Status { get; set; } = SessionStatus.IDLE;

    [JsonPropertyName("elapsedSeconds")]
    public double ElapsedSeconds { get; set; }
}
