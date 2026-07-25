using System;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models
{
    public class ProcessNode
    {
        public int ProcessId { get; set; }
        public int ParentProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
