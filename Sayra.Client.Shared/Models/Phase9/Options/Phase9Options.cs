using System;

namespace Sayra.Client.Shared.Models.Phase9.Options
{
    /// <summary>
    /// Configuration options for managing fleet workstation registration, dynamic collections, and sync timings.
    /// </summary>
    public class FleetOptions
    {
        /// <summary>
        /// Gets or sets the synchronization interval in seconds with the central fleet database.
        /// </summary>
        public int SyncIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets whether dynamic collection evaluations should run automatically upon workstation state change.
        /// </summary>
        public bool EnableAutoCollectionEvaluation { get; set; } = true;

        /// <summary>
        /// Gets or sets the default regional partition for new workstations.
        /// </summary>
        public string DefaultRegion { get; set; } = "Default";
    }

    /// <summary>
    /// Configuration options governing live telemetry rates and low-latency metrics push parameters.
    /// </summary>
    public class MonitoringOptions
    {
        /// <summary>
        /// Gets or sets live streaming sample collection rate in milliseconds.
        /// </summary>
        public int SamplingIntervalMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets maximum length of in-memory sliding telemetry buffers.
        /// </summary>
        public int TelemetryBufferSize { get; set; } = 500;

        /// <summary>
        /// Gets or sets whether to stream high-performance thread metrics.
        /// </summary>
        public bool StreamExtendedThreadMetrics { get; set; }
    }

    /// <summary>
    /// Configuration options for diagnostics scanning, reports storage, and package generation limits.
    /// </summary>
    public class DiagnosticsOptions
    {
        /// <summary>
        /// Gets or sets local path where diagnostic packages (.zip files) are constructed.
        /// </summary>
        public string LocalStagingDirectory { get; set; } = "Data/Diagnostics";

        /// <summary>
        /// Gets or sets maximum storage limit in megabytes allocated for local diagnostics.
        /// </summary>
        public long MaxDiagnosticsStorageMb { get; set; } = 500;

        /// <summary>
        /// Gets or sets the compression level utilized for constructing packages.
        /// </summary>
        public string CompressionLevel { get; set; } = "Optimal";
    }

    /// <summary>
    /// Configuration options for securing, throttling, and resuming general file transfers.
    /// </summary>
    public class TransferOptions
    {
        /// <summary>
        /// Gets or sets default chunk block size in bytes used for segmented transfers.
        /// </summary>
        public int DefaultChunkSizeBytes { get; set; } = 65536; // 64KB

        /// <summary>
        /// Gets or sets maximum parallel chunk downloads or uploads per file.
        /// </summary>
        public int MaxParallelTransfers { get; set; } = 4;

        /// <summary>
        /// Gets or sets bandwidth speed limit ceiling in bytes/sec. Zero represents unlimited.
        /// </summary>
        public long ThrottleRateBytesPerSec { get; set; }
    }

    /// <summary>
    /// Configuration options scheduling automated maintenance tasks and grace period timers.
    /// </summary>
    public class MaintenanceOptions
    {
        /// <summary>
        /// Gets or sets warning countdown timer duration in seconds shown to users prior to maintenance restart.
        /// </summary>
        public int WarnCountdownSeconds { get; set; } = 300;

        /// <summary>
        /// Gets or sets whether maintenance processes can force close active sessions.
        /// </summary>
        public bool ForceKillOnOverdue { get; set; } = true;

        /// <summary>
        /// Gets or sets local backup path prior to performing maintenance updates.
        /// </summary>
        public string BackupBeforeUpdateDir { get; set; } = "Data/Backups";
    }

    /// <summary>
    /// Configuration options for compiling, assigning, and evaluating registry/system policies.
    /// </summary>
    public class PolicyOptions
    {
        /// <summary>
        /// Gets or sets local path where policy templates are cached.
        /// </summary>
        public string PolicyCacheDirectory { get; set; } = "Data/Policies";

        /// <summary>
        /// Gets or sets interval in minutes to verify registry compliance against assigned rules.
        /// </summary>
        public int ComplianceEvaluationIntervalMinutes { get; set; } = 15;

        /// <summary>
        /// Gets or sets whether compliance violations should trigger automatic machine lockdown.
        /// </summary>
        public bool LockOnComplianceViolation { get; set; }
    }

    /// <summary>
    /// Configuration options for SQLCipher database administrative audits, rotation, and size ceilings.
    /// </summary>
    public class AuditOptions
    {
        /// <summary>
        /// Gets or sets maximum records count limit for in-memory audit logs.
        /// </summary>
        public int MaxInMemoryRecords { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether block signatures (cryptographic blockchains) are generated for historical audits.
        /// </summary>
        public bool EnableBlockVerification { get; set; } = true;

        /// <summary>
        /// Gets or sets retention period in days before old audit files are purged.
        /// </summary>
        public int AuditLogRetentionDays { get; set; } = 90;
    }

    /// <summary>
    /// Configuration options governing multi-machine parallel bulk operation execution structures.
    /// </summary>
    public class BulkOperationOptions
    {
        /// <summary>
        /// Gets or sets default parallel concurrency limit for dispatching bulk commands.
        /// </summary>
        public int DefaultConcurrencyLimit { get; set; } = 50;

        /// <summary>
        /// Gets or sets maximum execution timeout limit in seconds per target machine.
        /// </summary>
        public int TargetTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the percentage of failures allowed before a running bulk operation aborts.
        /// </summary>
        public int FailureAbortPercentageThreshold { get; set; } = 20;
    }

    /// <summary>
    /// Configuration options managing remote screen visual streaming and keyboard hook locks.
    /// </summary>
    public class RemoteSupportOptions
    {
        /// <summary>
        /// Gets or sets streaming framerate target (frames per second).
        /// </summary>
        public int TargetFps { get; set; } = 15;

        /// <summary>
        /// Gets or sets compression quality factor (0 to 100) for visual frames.
        /// </summary>
        public int VisualQuality { get; set; } = 75;

        /// <summary>
        /// Gets or sets whether standard keyboard shortcut locks (Alt+Tab, WinKey) are disabled during control.
        /// </summary>
        public bool OverrideKioskLockout { get; set; }
    }

    /// <summary>
    /// Configuration options for Administration API endpoints and TLS web routing.
    /// </summary>
    public class AdministrationOptions
    {
        /// <summary>
        /// Gets or sets central administration server URL.
        /// </summary>
        public string ServerUrl { get; set; } = "https://localhost:5001";

        /// <summary>
        /// Gets or sets maximum payload size limit in bytes accepted by administrative gateways.
        /// </summary>
        public long MaxRequestPayloadBytes { get; set; } = 10485760; // 10MB

        /// <summary>
        /// Gets or sets API Access Token for gateway handshakes.
        /// </summary>
        public string ApiAccessToken { get; set; } = string.Empty;
    }
}
