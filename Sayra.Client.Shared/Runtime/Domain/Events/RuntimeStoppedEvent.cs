using System;

namespace Sayra.Client.Shared.Runtime.Domain.Events
{
    public class RuntimeStoppedEvent
    {
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string? Reason { get; }

        public RuntimeStoppedEvent(string? reason = null)
        {
            Reason = reason;
        }
    }
}
