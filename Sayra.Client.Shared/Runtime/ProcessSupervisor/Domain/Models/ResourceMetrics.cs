using System;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models
{
    public class ResourceMetrics
    {
        public double CpuUsagePercentage { get; set; }
        public long MemoryUsageBytes { get; set; }
        public int HandleCount { get; set; }
    }
}
