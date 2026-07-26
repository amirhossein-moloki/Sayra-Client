using System;
using Sayra.Client.Shared.Runtime.Domain.States;

namespace Sayra.Client.Shared.Runtime.Domain.Events
{
    public class SessionExpiredEvent
    {
        public Guid SessionId { get; }
        public string UserId { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public RuntimeState State { get; }
        public string Details { get; }

        public SessionExpiredEvent(Guid sessionId, string userId, RuntimeState state, string details)
        {
            SessionId = sessionId;
            UserId = userId ?? string.Empty;
            State = state;
            Details = details ?? string.Empty;
        }
    }
}
