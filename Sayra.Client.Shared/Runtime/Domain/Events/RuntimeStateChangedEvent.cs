using System;
using Sayra.Client.Shared.Runtime.Domain.States;

namespace Sayra.Client.Shared.Runtime.Domain.Events
{
    public class RuntimeStateChangedEvent
    {
        public RuntimeState OldState { get; }
        public RuntimeState NewState { get; }
        public string? Reason { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public RuntimeStateChangedEvent(RuntimeState oldState, RuntimeState newState, string? reason = null)
        {
            OldState = oldState;
            NewState = newState;
            Reason = reason;
        }
    }
}
