using System;
using Sayra.Client.Shared.Runtime.Domain.States;

namespace Sayra.Client.Shared.Runtime.Domain.Events
{
    public class SessionWarningEvent
    {
        public Guid SessionId { get; }
        public string UserId { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public RuntimeState State { get; }
        public string Details { get; }
        public TimeSpan RemainingTime { get; }
        public int WarningLevel { get; }

        public SessionWarningEvent(Guid sessionId, string userId, RuntimeState state, string details, TimeSpan remainingTime, int warningLevel)
        {
            SessionId = sessionId;
            UserId = userId ?? string.Empty;
            State = state;
            Details = details ?? string.Empty;
            RemainingTime = remainingTime;
            WarningLevel = warningLevel;
        }
    }
}
