using System.Text.Json.Serialization;

namespace SayraClient.Models;

/// <summary>
/// Aligned with SayraConfigResponse schema from openapi.yaml
/// </summary>
public class ConfigModel
{
    [JsonPropertyName("heartbeat")]
    public HeartbeatConfig Heartbeat { get; set; } = new();

    [JsonPropertyName("session")]
    public SessionConfig Session { get; set; } = new();

    [JsonPropertyName("security")]
    public SecurityConfig Security { get; set; } = new();

    [JsonPropertyName("scaling")]
    public ScalingConfig Scaling { get; set; } = new();

    [JsonPropertyName("backup")]
    public BackupConfig Backup { get; set; } = new();
}

public class HeartbeatConfig
{
    [JsonPropertyName("intervalSeconds")]
    public int IntervalSeconds { get; set; }
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; }
}

public class SessionConfig
{
    [JsonPropertyName("maxConcurrentSessionsPerUser")]
    public int MaxConcurrentSessionsPerUser { get; set; }
    [JsonPropertyName("defaultSessionDurationMinutes")]
    public int DefaultSessionDurationMinutes { get; set; }
}

public class SecurityConfig
{
    [JsonPropertyName("maxAuthAttempts")]
    public int MaxAuthAttempts { get; set; }
    [JsonPropertyName("lockoutDurationMinutes")]
    public int LockoutDurationMinutes { get; set; }
    [JsonPropertyName("enforceSignedUpdates")]
    public bool EnforceSignedUpdates { get; set; }
}

public class ScalingConfig
{
    [JsonPropertyName("enableRedis")]
    public bool EnableRedis { get; set; }
    [JsonPropertyName("redisConnectionString")]
    public string RedisConnectionString { get; set; } = string.Empty;
}

public class BackupConfig
{
    [JsonPropertyName("backupIntervalHours")]
    public int BackupIntervalHours { get; set; }
    [JsonPropertyName("backupPath")]
    public string BackupPath { get; set; } = string.Empty;
    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; }
}
