using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models
{
    public class SystemDiagnosticsReport
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string MachineId { get; set; } = string.Empty;

        // Hardware Info
        public CpuDiagInfo Cpu { get; set; } = new();
        public List<GpuDiagInfo> Gpus { get; set; } = new();
        public MemoryDiagInfo Memory { get; set; } = new();
        public List<StorageDiagInfo> Storage { get; set; } = new();
        public List<NetworkDiagInfo> Networks { get; set; } = new();

        // Inventories
        public List<InstalledApplication> SoftwareInventory { get; set; } = new();
        public List<RunningProcess> ProcessInventory { get; set; } = new();
        public List<DriverInfo> DriverInventory { get; set; } = new();
    }

    public class CpuDiagInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public int LogicalCores { get; set; }
        public int PhysicalCores { get; set; }
        public double BaseClockGhz { get; set; }
    }

    public class GpuDiagInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string DriverVersion { get; set; } = string.Empty;
        public long DedicatedVramBytes { get; set; }
    }

    public class MemoryDiagInfo
    {
        public long TotalBytes { get; set; }
        public long AvailableBytes { get; set; }
        public string MemoryType { get; set; } = string.Empty;
        public int SpeedMhz { get; set; }
    }

    public class StorageDiagInfo
    {
        public string DriveLetter { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public long CapacityBytes { get; set; }
        public long FreeSizeBytes { get; set; }
        public string HealthStatus { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty; // SSD/HDD
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class NetworkDiagInfo
    {
        public string AdapterName { get; set; } = string.Empty;
        public string Ipv4Address { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public long SpeedBps { get; set; }
    }

    public class InstalledApplication
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string InstallPath { get; set; } = string.Empty;
    }

    public class RunningProcess
    {
        public int Pid { get; set; }
        public string Name { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty; // SHA-256
    }

    public class DriverInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
