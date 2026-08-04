using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Monitoring.Domain.Models
{
    /// <summary>
    /// Immutable real-time telemetry and state snapshot for a workstation managed by SAYRA.
    /// </summary>
    public record LiveMonitoringSnapshot
    {
        /// <summary>
        /// Gets the unique identifier for this snapshot.
        /// </summary>
        public Guid SnapshotId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Gets the target workstation's unique identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the timestamp when the snapshot was generated (in UTC).
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the version of the snapshot format.
        /// </summary>
        public string Version { get; init; } = "1.0.0";

        /// <summary>
        /// Gets the expiration timestamp for this snapshot to assist cache evictions.
        /// </summary>
        public DateTime ExpiresAtUtc { get; init; } = DateTime.UtcNow.AddMinutes(5);

        // --- PART 2: METRIC COLLECTORS ---

        /// <summary>
        /// Gets active CPU utilization percentage.
        /// </summary>
        public double CpuUsage { get; init; }

        /// <summary>
        /// Gets CPU clock frequency in GHz.
        /// </summary>
        public double CpuFrequencyGhz { get; init; }

        /// <summary>
        /// Gets CPU queue length or active load index.
        /// </summary>
        public double CpuLoad { get; init; }

        /// <summary>
        /// Gets memory utilization in Bytes.
        /// </summary>
        public double MemoryUsageBytes { get; init; }

        /// <summary>
        /// Gets memory pressure or saturation index percentage.
        /// </summary>
        public double MemoryPressurePercentage { get; init; }

        /// <summary>
        /// Gets disk bytes used.
        /// </summary>
        public double DiskUsageBytes { get; init; }

        /// <summary>
        /// Gets disk free space in bytes.
        /// </summary>
        public double DiskFreeSpaceBytes { get; init; }

        /// <summary>
        /// Gets active disk I/O activity percentage.
        /// </summary>
        public double DiskActivityPercentage { get; init; }

        /// <summary>
        /// Gets GPU core utilization percentage.
        /// </summary>
        public double GpuUsage { get; init; }

        /// <summary>
        /// Gets GPU VRAM usage in Bytes.
        /// </summary>
        public double GpuMemoryUsageBytes { get; init; }

        /// <summary>
        /// Gets GPU core temperature in degrees Celsius.
        /// </summary>
        public double GpuTemperatureCelsius { get; init; }

        /// <summary>
        /// Gets CPU core temperature in degrees Celsius.
        /// </summary>
        public double CpuTemperatureCelsius { get; init; }

        /// <summary>
        /// Gets motherboard sensor temperature in degrees Celsius.
        /// </summary>
        public double MotherboardTemperatureCelsius { get; init; }

        /// <summary>
        /// Gets network transmission upload speed in Bytes/sec.
        /// </summary>
        public double NetworkUploadBytesPerSec { get; init; }

        /// <summary>
        /// Gets network transmission download speed in Bytes/sec.
        /// </summary>
        public double NetworkDownloadBytesPerSec { get; init; }

        /// <summary>
        /// Gets overall network interface bandwidth utilization percentage.
        /// </summary>
        public double NetworkUtilizationPercentage { get; init; }

        /// <summary>
        /// Gets the active network adapter connection status (e.g. Connected, Disconnected).
        /// </summary>
        public string NetworkAdapterStatus { get; init; } = "Connected";

        /// <summary>
        /// Gets the ICMP or network endpoint latency in milliseconds.
        /// </summary>
        public double LatencyMs { get; init; }

        /// <summary>
        /// Gets the packet loss percentage of the connection.
        /// </summary>
        public double PacketLossPercentage { get; init; }

        /// <summary>
        /// Gets the packet latency jitter in milliseconds.
        /// </summary>
        public double JitterMs { get; init; }

        /// <summary>
        /// Gets the username of the active user session.
        /// </summary>
        public string CurrentUser { get; init; } = string.Empty;

        /// <summary>
        /// Gets list of active Windows sessions.
        /// </summary>
        public List<string> LoggedInSessions { get; init; } = new();

        /// <summary>
        /// Gets the duration of the current active session.
        /// </summary>
        public TimeSpan SessionDuration { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the title or identifier of the currently running foreground game.
        /// </summary>
        public string ActiveGame { get; init; } = string.Empty;

        /// <summary>
        /// Gets the structured summary of executing processes.
        /// </summary>
        public string RunningProcessesSummary { get; init; } = string.Empty;

        /// <summary>
        /// Gets status dictionary of background tasks and processes.
        /// </summary>
        public Dictionary<string, string> BackgroundServicesStatus { get; init; } = new();

        /// <summary>
        /// Gets status of important Windows Services.
        /// </summary>
        public Dictionary<string, string> WindowsServiceStatus { get; init; } = new();

        /// <summary>
        /// Gets the total active process count.
        /// </summary>
        public int ProcessCount { get; init; }

        /// <summary>
        /// Gets the total active thread count.
        /// </summary>
        public int ThreadCount { get; init; }

        /// <summary>
        /// Gets the total system handle count.
        /// </summary>
        public int HandleCount { get; init; }

        // --- PART 3: SYSTEM STATE ---

        /// <summary>
        /// Gets current overall machine operational status.
        /// </summary>
        public MachineStatus MachineStatus { get; init; } = MachineStatus.Offline;

        /// <summary>
        /// Gets connection stability state.
        /// </summary>
        public ConnectionStatus ConnectionStatus { get; init; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// Gets whether the workstation is open and available for user session requests.
        /// </summary>
        public bool IsAvailable { get; init; } = true;

        /// <summary>
        /// Gets whether the workstation is currently in an idle state.
        /// </summary>
        public bool IsIdle { get; init; }

        /// <summary>
        /// Gets whether the workstation is administratively locked.
        /// </summary>
        public bool IsLocked { get; init; }

        /// <summary>
        /// Gets whether the workstation is currently undergoing scheduled maintenance.
        /// </summary>
        public bool IsInMaintenance { get; init; }

        /// <summary>
        /// Gets whether the workstation is running automated self-healing recoveries.
        /// </summary>
        public bool IsInRecovery { get; init; }

        /// <summary>
        /// Gets current system power status (e.g. AC Power, Battery).
        /// </summary>
        public string PowerState { get; init; } = "AC Power";

        /// <summary>
        /// Gets current update management status.
        /// </summary>
        public string UpdateState { get; init; } = "UpToDate";

        /// <summary>
        /// Gets evaluated workstation health level.
        /// </summary>
        public MachineHealthStatus OverallHealth { get; init; } = MachineHealthStatus.Healthy;

        /// <summary>
        /// Gets calculated mathematical health score (0.0 to 100.0).
        /// </summary>
        public double OverallHealthScore { get; init; } = 100.0;
    }

    /// <summary>
    /// Represents the delta comparison between two chronological workstation snapshots.
    /// </summary>
    public record LiveMonitoringDeltaSnapshot
    {
        /// <summary>
        /// Gets the difference in CPU utilization.
        /// </summary>
        public double CpuUsageDelta { get; init; }

        /// <summary>
        /// Gets the difference in memory utilization percentage.
        /// </summary>
        public double MemoryPressureDelta { get; init; }

        /// <summary>
        /// Gets the difference in disk utilization or activity.
        /// </summary>
        public double DiskActivityDelta { get; init; }

        /// <summary>
        /// Gets the difference in network throughput speed.
        /// </summary>
        public double NetworkThroughputDelta { get; init; }

        /// <summary>
        /// Gets whether the overall machine status changed.
        /// </summary>
        public bool StatusChanged { get; init; }

        /// <summary>
        /// Gets the old machine status before change.
        /// </summary>
        public MachineStatus PreviousMachineStatus { get; init; }

        /// <summary>
        /// Gets the new machine status after change.
        /// </summary>
        public MachineStatus NewMachineStatus { get; init; }

        /// <summary>
        /// Gets whether the calculated health state changed.
        /// </summary>
        public bool HealthChanged { get; init; }

        /// <summary>
        /// Gets the old health status level.
        /// </summary>
        public MachineHealthStatus PreviousHealth { get; init; }

        /// <summary>
        /// Gets the new health status level.
        /// </summary>
        public MachineHealthStatus NewHealth { get; init; }
    }

    /// <summary>
    /// Represents a security/integrity state snapshot of a workstation.
    /// </summary>
    public record SecureMonitoringContext
    {
        /// <summary>
        /// Gets the unique session tracking token.
        /// </summary>
        public string SessionToken { get; init; } = string.Empty;

        /// <summary>
        /// Gets the active operator identifier.
        /// </summary>
        public string OperatorId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the security nonce utilized for replay safety checks.
        /// </summary>
        public string Nonce { get; init; } = string.Empty;

        /// <summary>
        /// Gets the creation timestamp.
        /// </summary>
        public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets whether the caller possesses full control administrative permissions.
        /// </summary>
        public bool IsAuthorized { get; init; }
    }

    /// <summary>
    /// Configuration model representing bounds and constraints of a single metric threshold.
    /// </summary>
    public record ThresholdConfig
    {
        /// <summary>
        /// Gets the warning value threshold limit.
        /// </summary>
        public double WarningLimit { get; init; }

        /// <summary>
        /// Gets the critical value threshold limit.
        /// </summary>
        public double CriticalLimit { get; init; }

        /// <summary>
        /// Gets the emergency value threshold limit.
        /// </summary>
        public double EmergencyLimit { get; init; }

        /// <summary>
        /// Gets whether higher values represent violation (true) or lower values (false).
        /// </summary>
        public bool ViolateOnHigher { get; init; } = true;
    }
}
