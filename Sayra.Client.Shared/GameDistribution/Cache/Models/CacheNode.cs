using System;

namespace Sayra.Client.Shared.GameDistribution.Cache.Models
{
    public class CacheNode
    {
        public string NodeId { get; set; } = string.Empty;
        public string MachineId { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
        public long FreeStorageBytes { get; set; }
        public bool IsSsd { get; set; }
        public double NetworkSpeedMbps { get; set; }
        public double CpuLoadPercent { get; set; }
        public double CacheCompletenessPercent { get; set; }
        public double HealthScore { get; set; } = 100.0;
    }
}
