using System;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Events
{
    public class UnauthorizedChildProcessEvent
    {
        public Guid RuntimeId { get; }
        public int ProcessId { get; }
        public DateTime Timestamp { get; }
        public string Details { get; }

        public UnauthorizedChildProcessEvent(Guid runtimeId, int processId, string details = "")
        {
            RuntimeId = runtimeId;
            ProcessId = processId;
            Timestamp = DateTime.UtcNow;
            Details = details;
        }
    }
}
