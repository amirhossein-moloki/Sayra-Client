using System;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Events
{
    public class LaunchFailedEvent
    {
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string GameId { get; }
        public Guid SessionId { get; }
        public string Reason { get; }

        public LaunchFailedEvent(string gameId, Guid sessionId, string reason)
        {
            GameId = gameId;
            SessionId = sessionId;
            Reason = reason;
        }
    }
}
