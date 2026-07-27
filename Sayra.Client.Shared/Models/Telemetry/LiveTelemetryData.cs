using System;

namespace Sayra.Client.Shared.Models
{
    public class LiveTelemetryData
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string MachineId { get; set; } = string.Empty;
        public double UptimeSeconds { get; set; }

        // Performance
        public double CpuUsagePercent { get; set; }
        public double RamUsedMb { get; set; }
        public double RamTotalMb { get; set; }
        public double GpuUsagePercent { get; set; }
        public double VramUsedMb { get; set; }
        public double VramTotalMb { get; set; }
        public double Fps { get; set; }

        // Hardware
        public double CpuTemperature { get; set; }
        public double GpuTemperature { get; set; }
        public double FanSpeed { get; set; }

        // Network
        public double BytesSentPerSecond { get; set; }
        public double BytesReceivedPerSecond { get; set; }
        public double PingMs { get; set; }

        // Storage
        public double DiskReadBytesPerSecond { get; set; }
        public double DiskWriteBytesPerSecond { get; set; }
        public double FreeSpaceGb { get; set; }

        // Session
        public string LoggedUser { get; set; } = string.Empty;
        public int WindowsSessionId { get; set; }
        public string ActiveProcess { get; set; } = string.Empty;
        public string KioskState { get; set; } = string.Empty;
    }
}
