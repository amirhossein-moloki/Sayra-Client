using System;

namespace Sayra.Client.Launcher.Models
{
    public class ProcessStatistics
    {
        public int Pid { get; set; }
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public double CpuUsagePercentage { get; set; }
        public double RamUsageMb { get; set; }
        public TimeSpan RunningDuration { get; set; }
        public int? ExitCode { get; set; }
        public bool HasCrashed { get; set; }
        public string CrashReason { get; set; } = string.Empty;
    }
}
