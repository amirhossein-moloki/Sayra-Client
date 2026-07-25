using System;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models
{
    public class ProcessInfo
    {
        public Guid RuntimeId { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
    }
}
