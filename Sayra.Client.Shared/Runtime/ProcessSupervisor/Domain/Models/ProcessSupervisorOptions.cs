using System;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models
{
    public class ProcessSupervisorOptions
    {
        /// <summary>
        /// Maximum physical memory in bytes allowed for the Job Object. 0 means unlimited.
        /// </summary>
        public long MaxMemoryBytes { get; set; } = 0;

        /// <summary>
        /// CPU affinity mask for processes assigned to the Job Object. 0 means all processors are available.
        /// </summary>
        public ulong CpuAffinityMask { get; set; } = 0;

        /// <summary>
        /// Desired process priority class name (e.g. Normal, High, Idle, AboveNormal, BelowNormal, RealTime).
        /// </summary>
        public string PriorityClass { get; set; } = "Normal";
    }
}
