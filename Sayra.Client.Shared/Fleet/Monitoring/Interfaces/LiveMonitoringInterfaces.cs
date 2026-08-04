using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Fleet.Monitoring.Domain.Models;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Monitoring.Interfaces
{
    /// <summary>
    /// Builder class representing a mutable workspace used by collectors to build a <see cref="LiveMonitoringSnapshot"/> with low allocations.
    /// </summary>
    public class LiveMonitoringSnapshotBuilder
    {
        /// <summary>
        /// Gets or sets the target machine identifier.
        /// </summary>
        public string MachineId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the snapshot generation timestamp.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets CPU utilization percentage.
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// Gets or sets CPU clock frequency.
        /// </summary>
        public double CpuFrequencyGhz { get; set; }

        /// <summary>
        /// Gets or sets CPU load index.
        /// </summary>
        public double CpuLoad { get; set; }

        /// <summary>
        /// Gets or sets RAM usage in bytes.
        /// </summary>
        public double MemoryUsageBytes { get; set; }

        /// <summary>
        /// Gets or sets RAM pressure or saturation index percentage.
        /// </summary>
        public double MemoryPressurePercentage { get; set; }

        /// <summary>
        /// Gets or sets disk bytes used.
        /// </summary>
        public double DiskUsageBytes { get; set; }

        /// <summary>
        /// Gets or sets disk free space in bytes.
        /// </summary>
        public double DiskFreeSpaceBytes { get; set; }

        /// <summary>
        /// Gets or sets active disk I/O activity percentage.
        /// </summary>
        public double DiskActivityPercentage { get; set; }

        /// <summary>
        /// Gets or sets GPU utilization percentage.
        /// </summary>
        public double GpuUsage { get; set; }

        /// <summary>
        /// Gets or sets GPU VRAM usage in Bytes.
        /// </summary>
        public double GpuMemoryUsageBytes { get; set; }

        /// <summary>
        /// Gets or sets GPU core temperature in degrees Celsius.
        /// </summary>
        public double GpuTemperatureCelsius { get; set; }

        /// <summary>
        /// Gets or sets CPU core temperature in degrees Celsius.
        /// </summary>
        public double CpuTemperatureCelsius { get; set; }

        /// <summary>
        /// Gets or sets motherboard sensor temperature in degrees Celsius.
        /// </summary>
        public double MotherboardTemperatureCelsius { get; set; }

        /// <summary>
        /// Gets or sets network transmission upload speed in Bytes/sec.
        /// </summary>
        public double NetworkUploadBytesPerSec { get; set; }

        /// <summary>
        /// Gets or sets network transmission download speed in Bytes/sec.
        /// </summary>
        public double NetworkDownloadBytesPerSec { get; set; }

        /// <summary>
        /// Gets or sets overall network bandwidth utilization percentage.
        /// </summary>
        public double NetworkUtilizationPercentage { get; set; }

        /// <summary>
        /// Gets or sets active network adapter status.
        /// </summary>
        public string NetworkAdapterStatus { get; set; } = "Connected";

        /// <summary>
        /// Gets or sets ICMP endpoint latency in milliseconds.
        /// </summary>
        public double LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets network packet loss percentage.
        /// </summary>
        public double PacketLossPercentage { get; set; }

        /// <summary>
        /// Gets or sets packet latency jitter in milliseconds.
        /// </summary>
        public double JitterMs { get; set; }

        /// <summary>
        /// Gets or sets username of the active user session.
        /// </summary>
        public string CurrentUser { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets list of active Windows sessions.
        /// </summary>
        public List<string> LoggedInSessions { get; set; } = new();

        /// <summary>
        /// Gets or sets the active user session duration.
        /// </summary>
        public TimeSpan SessionDuration { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Gets or sets title of currently running foreground game.
        /// </summary>
        public string ActiveGame { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets summary list of active executing processes.
        /// </summary>
        public string RunningProcessesSummary { get; set; } = string.Empty;

        /// <summary>
        /// Gets background task statuses.
        /// </summary>
        public ConcurrentDictionary<string, string> BackgroundServicesStatus { get; } = new();

        /// <summary>
        /// Gets important Windows service statuses.
        /// </summary>
        public ConcurrentDictionary<string, string> WindowsServiceStatus { get; } = new();

        /// <summary>
        /// Gets or sets total active process count.
        /// </summary>
        public int ProcessCount { get; set; }

        /// <summary>
        /// Gets or sets total active thread count.
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Gets or sets total system handle count.
        /// </summary>
        public int HandleCount { get; set; }

        /// <summary>
        /// Gets or sets machine operational status.
        /// </summary>
        public MachineStatus MachineStatus { get; set; } = MachineStatus.Offline;

        /// <summary>
        /// Gets or sets connection health state.
        /// </summary>
        public ConnectionStatus ConnectionStatus { get; set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// Gets or sets whether the workstation is open for user sessions.
        /// </summary>
        public bool IsAvailable { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the workstation is idle.
        /// </summary>
        public bool IsIdle { get; set; }

        /// <summary>
        /// Gets or sets whether the workstation is locked.
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Gets or sets whether the workstation is in maintenance mode.
        /// </summary>
        public bool IsInMaintenance { get; set; }

        /// <summary>
        /// Gets or sets whether self-healing recoveries are active.
        /// </summary>
        public bool IsInRecovery { get; set; }

        /// <summary>
        /// Gets or sets current system power status.
        /// </summary>
        public string PowerState { get; set; } = "AC Power";

        /// <summary>
        /// Gets or sets update status.
        /// </summary>
        public string UpdateState { get; set; } = "UpToDate";

        /// <summary>
        /// Gets or sets evaluated health level.
        /// </summary>
        public MachineHealthStatus OverallHealth { get; set; } = MachineHealthStatus.Healthy;

        /// <summary>
        /// Gets or sets calculated mathematical health score.
        /// </summary>
        public double OverallHealthScore { get; set; } = 100.0;

        /// <summary>
        /// Compiles the builder's state into an immutable <see cref="LiveMonitoringSnapshot"/> instance.
        /// </summary>
        public LiveMonitoringSnapshot Build()
        {
            return new LiveMonitoringSnapshot
            {
                MachineId = MachineId,
                TimestampUtc = TimestampUtc,
                CpuUsage = CpuUsage,
                CpuFrequencyGhz = CpuFrequencyGhz,
                CpuLoad = CpuLoad,
                MemoryUsageBytes = MemoryUsageBytes,
                MemoryPressurePercentage = MemoryPressurePercentage,
                DiskUsageBytes = DiskUsageBytes,
                DiskFreeSpaceBytes = DiskFreeSpaceBytes,
                DiskActivityPercentage = DiskActivityPercentage,
                GpuUsage = GpuUsage,
                GpuMemoryUsageBytes = GpuMemoryUsageBytes,
                GpuTemperatureCelsius = GpuTemperatureCelsius,
                CpuTemperatureCelsius = CpuTemperatureCelsius,
                MotherboardTemperatureCelsius = MotherboardTemperatureCelsius,
                NetworkUploadBytesPerSec = NetworkUploadBytesPerSec,
                NetworkDownloadBytesPerSec = NetworkDownloadBytesPerSec,
                NetworkUtilizationPercentage = NetworkUtilizationPercentage,
                NetworkAdapterStatus = NetworkAdapterStatus,
                LatencyMs = LatencyMs,
                PacketLossPercentage = PacketLossPercentage,
                JitterMs = JitterMs,
                CurrentUser = CurrentUser,
                LoggedInSessions = new List<string>(LoggedInSessions),
                SessionDuration = SessionDuration,
                ActiveGame = ActiveGame,
                RunningProcessesSummary = RunningProcessesSummary,
                BackgroundServicesStatus = new Dictionary<string, string>(BackgroundServicesStatus),
                WindowsServiceStatus = new Dictionary<string, string>(WindowsServiceStatus),
                ProcessCount = ProcessCount,
                ThreadCount = ThreadCount,
                HandleCount = HandleCount,
                MachineStatus = MachineStatus,
                ConnectionStatus = ConnectionStatus,
                IsAvailable = IsAvailable,
                IsIdle = IsIdle,
                IsLocked = IsLocked,
                IsInMaintenance = IsInMaintenance,
                IsInRecovery = IsInRecovery,
                PowerState = PowerState,
                UpdateState = UpdateState,
                OverallHealth = OverallHealth,
                OverallHealthScore = OverallHealthScore
            };
        }
    }

    /// <summary>
    /// Contract representing a pluggable real-time metric and status collector.
    /// </summary>
    public interface ILiveMetricCollector
    {
        /// <summary>
        /// Gets the distinct name of the metrics or state collected.
        /// </summary>
        string MetricName { get; }

        /// <summary>
        /// Asynchronously executes collection and updates the provided mutable builder.
        /// </summary>
        Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default);
    }

    /// <summary>
    /// Pipeline orchestrating the processing, validation, tag enrichment, and threshold evaluations of snapshots.
    /// </summary>
    public interface IMonitoringPipeline
    {
        /// <summary>
        /// Processes a newly generated snapshot, performing validations and threshold evaluations.
        /// </summary>
        Task<LiveMonitoringSnapshot> ProcessSnapshotAsync(LiveMonitoringSnapshot snapshot, CancellationToken ct = default);
    }

    /// <summary>
    /// Coordinator managing scheduled recurring loops, state tracking, and status synchronizations.
    /// </summary>
    public interface IMonitoringScheduler
    {
        /// <summary>
        /// Starts scheduled polling for the given workstation machine.
        /// </summary>
        void StartPolling(string machineId);

        /// <summary>
        /// Stops scheduled polling for the given workstation machine.
        /// </summary>
        void StopPolling(string machineId);

        /// <summary>
        /// Triggers an immediate manual polling and snapshot refresh.
        /// </summary>
        Task<LiveMonitoringSnapshot> TriggerManualRefreshAsync(string machineId, CancellationToken ct = default);
    }

    /// <summary>
    /// Engine adjusting sampling frequency based on adaptive load or burst-triggered intervals.
    /// </summary>
    public interface ISamplingEngine
    {
        /// <summary>
        /// Returns the optimal dynamic sampling interval in milliseconds for a machine based on active context.
        /// </summary>
        int GetSamplingIntervalMs(string machineId);

        /// <summary>
        /// Initiates a high-frequency burst sampling window for a machine.
        /// </summary>
        void TriggerBurstSampling(string machineId, TimeSpan duration);

        /// <summary>
        /// Registers a dynamic update event indicating machine load shifts.
        /// </summary>
        void UpdateLoadState(string machineId, bool isHighLoad);
    }

    /// <summary>
    /// Coordinator invoking all registered metric collectors concurrently to compile a raw snapshot.
    /// </summary>
    public interface IPollingEngine
    {
        /// <summary>
        /// Executes all registered metric collectors to assemble raw workstation telemetry.
        /// </summary>
        Task<LiveMonitoringSnapshot> PollMetricsAsync(string machineId, CancellationToken ct = default);
    }

    /// <summary>
    /// Service managing creation, comparison, historical series, and compression of workstation snapshots.
    /// </summary>
    public interface ISnapshotEngine
    {
        /// <summary>
        /// Compiles the differences between two consecutive snapshots.
        /// </summary>
        LiveMonitoringDeltaSnapshot ComputeDelta(LiveMonitoringSnapshot current, LiveMonitoringSnapshot previous);

        /// <summary>
        /// Compiles a set of historical snapshots into an averaged, aggregated snapshot.
        /// </summary>
        LiveMonitoringSnapshot CompileAggregate(string machineId, IEnumerable<LiveMonitoringSnapshot> history);

        /// <summary>
        /// Compresses a snapshot payload using standard GZip stream compression.
        /// </summary>
        byte[] CompressSnapshot(LiveMonitoringSnapshot snapshot);

        /// <summary>
        /// Decompresses a compressed snapshot payload.
        /// </summary>
        LiveMonitoringSnapshot DecompressSnapshot(byte[] compressedData);
    }

    /// <summary>
    /// Engine calculating averages, moving averages, standard percentiles, and trend/change detections.
    /// </summary>
    public interface IAggregationEngine
    {
        /// <summary>
        /// Computes moving average for a series of values in a rolling window.
        /// </summary>
        double ComputeMovingAverage(IEnumerable<double> values);

        /// <summary>
        /// Computes any arbitrary percentile (e.g. 90th, 95th, 99th) over a series of metric samples.
        /// </summary>
        double ComputePercentile(IEnumerable<double> values, double percentile);

        /// <summary>
        /// Analyzes a series of historical readings to determine the current trend trajectory.
        /// </summary>
        string DetectTrend(IEnumerable<double> values);

        /// <summary>
        /// Evaluates a metric series for anomalous spikes exceeding statistical standard deviations.
        /// </summary>
        bool DetectPeak(IEnumerable<double> values, double currentValue, double thresholdStandardDeviations);
    }

    /// <summary>
    /// In-memory cache manager storing expiring telemetry, snapshots, health, and historical series.
    /// </summary>
    public interface IMonitoringCache
    {
        /// <summary>
        /// Caches the latest snapshot for a machine.
        /// </summary>
        void SetSnapshot(string machineId, LiveMonitoringSnapshot snapshot);

        /// <summary>
        /// Retrieves the latest cached snapshot.
        /// </summary>
        LiveMonitoringSnapshot? GetSnapshot(string machineId);

        /// <summary>
        /// Retrieves all latest cached snapshots.
        /// </summary>
        IReadOnlyList<LiveMonitoringSnapshot> GetAllSnapshots();

        /// <summary>
        /// Retrieves chronological history of cached snapshots for a machine.
        /// </summary>
        IReadOnlyList<LiveMonitoringSnapshot> GetHistory(string machineId);

        /// <summary>
        /// Invalidates cached entries and sweeps expired elements.
        /// </summary>
        void Invalidate(string machineId);

        /// <summary>
        /// Runs global memory optimization, pruning oldest records beyond configured limits.
        /// </summary>
        void OptimizeMemoryUsage();
    }

    /// <summary>
    /// Evaluator matching live metrics against Warning, Critical, and Emergency thresholds.
    /// </summary>
    public interface IThresholdEvaluator
    {
        /// <summary>
        /// Registers a customized threshold configuration for a specific metric.
        /// </summary>
        void ConfigureThreshold(string metricName, ThresholdConfig config);

        /// <summary>
        /// Evaluates a collected metric value, returning the determined severity level.
        /// </summary>
        MachineHealthStatus Evaluate(string machineId, string metricName, double value, out double limitValue);
    }

    /// <summary>
    /// API Query Service allowing advanced pagination, sorting, and filtering over cached live telemetry.
    /// </summary>
    public interface ILiveMonitoringQueryService
    {
        /// <summary>
        /// Queries the latest metric values across a filtered set of workstations.
        /// </summary>
        Task<IReadOnlyList<LiveMonitoringSnapshot>> QueryCurrentMetricsAsync(
            Func<LiveMonitoringSnapshot, bool>? filter = null,
            string? sortBy = null,
            bool ascending = true,
            int pageIndex = 0,
            int pageSize = 10,
            CancellationToken ct = default);

        /// <summary>
        /// Retrieves historical telemetry snapshots for a workstation with optional date limits and pagination.
        /// </summary>
        Task<IReadOnlyList<LiveMonitoringSnapshot>> QueryHistoricalMetricsAsync(
            string machineId,
            DateTime? from = null,
            DateTime? to = null,
            int pageIndex = 0,
            int pageSize = 10,
            CancellationToken ct = default);

        /// <summary>
        /// Computes trend analyses and aggregated statistics for a target workstation metric.
        /// </summary>
        Task<IDictionary<string, object>> QueryTrendsAsync(string machineId, string metricName, CancellationToken ct = default);
    }

    /// <summary>
    /// Service establishing secure administrative context, permissions verification, and visibility boundaries.
    /// </summary>
    public interface ILiveMonitoringSecurityService
    {
        /// <summary>
        /// Validates that the active operator context is authorized to view telemetry for the specified machine.
        /// </summary>
        Task<bool> ValidateVisibilityAsync(SecureMonitoringContext context, string machineId, CancellationToken ct = default);

        /// <summary>
        /// Verifies specific execution permissions for interactive monitoring overrides.
        /// </summary>
        Task<bool> CheckPermissionAsync(SecureMonitoringContext context, string requiredPermission, CancellationToken ct = default);
    }
}
