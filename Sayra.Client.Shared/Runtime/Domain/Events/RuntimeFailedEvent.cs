using System;

namespace Sayra.Client.Shared.Runtime.Domain.Events
{
    public class RuntimeFailedEvent
    {
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string? Reason { get; }

        public RuntimeFailedEvent(string? reason = null)
        {
            Reason = reason;
        }
    }
}
