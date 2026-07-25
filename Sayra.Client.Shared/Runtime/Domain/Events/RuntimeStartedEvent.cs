using System;

namespace Sayra.Client.Shared.Runtime.Domain.Events
{
    public class RuntimeStartedEvent
    {
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string? Reason { get; }

        public RuntimeStartedEvent(string? reason = null)
        {
            Reason = reason;
        }
    }
}
