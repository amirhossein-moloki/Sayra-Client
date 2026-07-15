using System;
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

    // Game Specific telemetry metrics
    [JsonPropertyName("runningGameName")]
    public string RunningGameName { get; set; } = string.Empty;

    [JsonPropertyName("runningGamePid")]
    public int RunningGamePid { get; set; }

    [JsonPropertyName("runningGameCpu")]
    public double RunningGameCpu { get; set; }

    [JsonPropertyName("runningGameRam")]
    public double RunningGameRam { get; set; }

    [JsonPropertyName("runningGameDurationSeconds")]
    public double RunningGameDurationSeconds { get; set; }

    [JsonPropertyName("totalLaunches")]
    public int TotalLaunches { get; set; }

    [JsonPropertyName("totalCrashes")]
    public int TotalCrashes { get; set; }

    [JsonPropertyName("totalRestarts")]
    public int TotalRestarts { get; set; }
}
