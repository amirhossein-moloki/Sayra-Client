using System.Text.Json.Serialization;

namespace SayraClient.Models;

/// <summary>
/// Aligned with SessionResponse schema from openapi.yaml
/// </summary>
public class SessionModel
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("pcId")]
    public string PcId { get; set; } = string.Empty;

    [JsonPropertyName("siteId")]
    public string SiteId { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "IDLE";

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("currentCost")]
    public double CurrentCost { get; set; }

    [JsonPropertyName("ratePerHour")]
    public double RatePerHour { get; set; }

    // Internal helper for tracking elapsed time locally, not part of OpenAPI SessionResponse
    [JsonIgnore]
    public double ElapsedSeconds { get; set; }
}
