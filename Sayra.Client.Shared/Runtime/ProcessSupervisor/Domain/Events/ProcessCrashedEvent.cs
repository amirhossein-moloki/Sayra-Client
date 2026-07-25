using System;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Events
{
    public class ProcessCrashedEvent
    {
        public Guid RuntimeId { get; }
        public int ProcessId { get; }
        public DateTime Timestamp { get; }
        public string Details { get; }
        public int ExitCode { get; }

        public ProcessCrashedEvent(Guid runtimeId, int processId, int exitCode, string details = "")
        {
            RuntimeId = runtimeId;
            ProcessId = processId;
            ExitCode = exitCode;
            Timestamp = DateTime.UtcNow;
            Details = details;
        }
    }
}
