using System;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.States;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models
{
    public class ProcessStatus
    {
        public Guid RuntimeId { get; set; }
        public int ProcessId { get; set; }
        public ProcessState State { get; set; }
        public DateTime StartTime { get; set; }
        public string Details { get; set; } = string.Empty;
    }
}
