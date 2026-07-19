using System;

namespace Sayra.Client.Launcher.Events
{
    public class GameLaunchingEventArgs : EventArgs
    {
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class GameStartedEventArgs : EventArgs
    {
        public int Pid { get; set; }
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class GameExitedEventArgs : EventArgs
    {
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class GameCrashedEventArgs : EventArgs
    {
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class GameRestartedEventArgs : EventArgs
    {
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int RetryCount { get; set; }
    }

    public class GameKilledEventArgs : EventArgs
    {
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Pid { get; set; }
    }

    public class LaunchFailedEventArgs : EventArgs
    {
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
