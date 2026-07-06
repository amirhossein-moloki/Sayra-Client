using System;

namespace Sayra.Client.Shared.Models
{
    public enum ClientCoreState
    {
        STARTING,
        CONNECTING,
        AUTHENTICATING,
        READY,
        IN_SESSION,
        DISCONNECTED,
        RECOVERING
    }

    public enum SessionStatus
    {
        IDLE,
        ACTIVE,
        PAUSED,
        ENDED
    }

    public class ClientStateDto
    {
        public ClientCoreState CoreState { get; set; }
        public SessionStatus SessionStatus { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public DateTime? StartTime { get; set; }
        public double ElapsedSeconds { get; set; }
        public double TotalDurationMinutes { get; set; }
        public double RatePerHour { get; set; }
        public double CurrentCost { get; set; }
        public string? UserName { get; set; }
        public bool IsKioskLocked { get; set; }
    }

    public enum LaunchPolicy
    {
        User,
        Admin,
        Restricted
    }

    public class GameModel
    {
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public LaunchPolicy LaunchPolicy { get; set; } = LaunchPolicy.User;
        public bool IsFavorite { get; set; }
    }

    public class AppDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
    }

    public class SecurityEventPayload
    {
        public string EventType { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium";
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Details { get; set; }
    }

    public class UpdateManifest
    {
        public string Version { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string PackageUrl { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty; // SHA256
        public string Signature { get; set; } = string.Empty; // RSA Signature of the checksum or manifest
        public bool IsCritical { get; set; }
        public DateTime ReleaseDate { get; set; }
    }

    public class UpdateProgressPayload
    {
        public string Version { get; set; } = string.Empty;
        public double ProgressPercentage { get; set; }
        public string CurrentAction { get; set; } = string.Empty;
    }
}
