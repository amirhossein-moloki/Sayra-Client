using System;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Events
{
    public class LaunchCompletedEvent
    {
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string GameId { get; }
        public Guid SessionId { get; }
        public int ProcessId { get; }

        public LaunchCompletedEvent(string gameId, Guid sessionId, int processId)
        {
            GameId = gameId;
            SessionId = sessionId;
            ProcessId = processId;
        }
    }
}
