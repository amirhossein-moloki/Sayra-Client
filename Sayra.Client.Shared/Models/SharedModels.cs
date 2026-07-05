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
}
