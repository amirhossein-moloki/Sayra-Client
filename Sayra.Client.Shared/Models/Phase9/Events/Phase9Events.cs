using System;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Models.Phase9.Events
{
    /// <summary>
    /// Base event class for all Phase 9 system and domain events.
    /// </summary>
    public abstract record Phase9BaseEvent
    {
        /// <summary>
        /// Gets the unique tracking correlation identifier.
        /// </summary>
        public Guid EventId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Gets the timestamp when the event was published/dispatched.
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event triggered when a machine successfully authenticates and establishes contact.
    /// </summary>
    public record MachineConnected(string MachineId, string IpAddress) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a machine drops connection or goes offline.
    /// </summary>
    public record MachineDisconnected(string MachineId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a machine health score or health status tier updates.
    /// </summary>
    public record MachineHealthChanged(string MachineId, MachineHealthStatus OldStatus, MachineHealthStatus NewStatus, double NewScore) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a new workstation is registered in the fleet database.
    /// </summary>
    public record MachineRegistered(string MachineId, string Hostname, string IpAddress) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a workstation is removed from the fleet database.
    /// </summary>
    public record MachineRemoved(string MachineId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a workstation goes online.
    /// </summary>
    public record MachineOnline(string MachineId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a workstation goes offline.
    /// </summary>
    public record MachineOffline(string MachineId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a tag is assigned to a workstation.
    /// </summary>
    public record TagAssigned(string MachineId, string Key, string Value) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a tag is removed from a workstation.
    /// </summary>
    public record TagRemoved(string MachineId, string Key) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a workstation's hardware/software inventory is updated.
    /// </summary>
    public record InventoryUpdated(string MachineId, MachineInventory Inventory) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a workstation's system version changes.
    /// </summary>
    public record VersionChanged(string MachineId, MachineVersion Version) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a new fleet group (static or dynamic) is created.
    /// </summary>
    public record FleetGroupCreated(string GroupId, string Name, FleetGroupType GroupType) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a fleet group is removed from the system.
    /// </summary>
    public record FleetGroupDeleted(string GroupId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote control command is queued for execution.
    /// </summary>
    public record CommandQueued(string CommandId, string TargetMachineId, string Action, CommandPriority Priority) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote command starts active execution.
    /// </summary>
    public record CommandStarted(string CommandId, string TargetMachineId, string Action) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote command finishes execution successfully.
    /// </summary>
    public record CommandCompleted(string CommandId, string TargetMachineId, string Action, OperationResult Outcome) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a queued or executing remote command is cancelled.
    /// </summary>
    public record CommandCancelled(string CommandId, string TargetMachineId, string Action) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote command execution fails.
    /// </summary>
    public record CommandFailed(string CommandId, string TargetMachineId, string Action, string ErrorMessage) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote command is dispatched to target endpoints.
    /// </summary>
    public record CommandDispatched(string CommandId, string TargetMachineId, string Action) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote command is accepted by target handlers.
    /// </summary>
    public record CommandAccepted(string CommandId, string TargetMachineId, string Action) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote command is rejected during validation or authorization.
    /// </summary>
    public record CommandRejected(string CommandId, string TargetMachineId, string Action, string Reason) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a transient error retry attempt starts.
    /// </summary>
    public record RetryStarted(string CommandId, string TargetMachineId, string Action, int AttemptNumber) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a transient error retry succeeds.
    /// </summary>
    public record RetryCompleted(string CommandId, string TargetMachineId, string Action, int AttemptNumber) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote command execution deadline is exceeded.
    /// </summary>
    public record TimeoutOccurred(string CommandId, string TargetMachineId, string Action) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a multi-machine bulk execution context begins.
    /// </summary>
    public record BulkOperationStarted(string BulkOperationId, string Action, int TargetCount) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a bulk operation completes on all targets.
    /// </summary>
    public record BulkOperationCompleted(string BulkOperationId, string Action, int SucceededCount, int FailedCount) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a bulk operation fails completely or is aborted.
    /// </summary>
    public record BulkOperationFailed(string BulkOperationId, string Action, string ErrorMessage) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a policy is assigned to a target workstation or group scope.
    /// </summary>
    public record PolicyAssigned(string AssignmentId, string TargetId, string PolicyId, string VersionTag) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a policy assignment is removed.
    /// </summary>
    public record PolicyRemoved(string AssignmentId, string TargetId, string PolicyId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a workstation compliance check detects policy violations.
    /// </summary>
    public record PolicyViolationDetected(string MachineId, string PolicyId, string ViolatingKeysDescription) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a scheduled workstation maintenance window begins.
    /// </summary>
    public record MaintenanceStarted(string ScheduleId, string WindowId, MaintenanceWindowType Category) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when maintenance successfully completes.
    /// </summary>
    public record MaintenanceCompleted(string ScheduleId, string WindowId, MaintenanceStatus State) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when maintenance is cancelled or bypassed.
    /// </summary>
    public record MaintenanceCancelled(string ScheduleId, string WindowId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote desktop assistance support session starts streaming.
    /// </summary>
    public record RemoteSessionStarted(string SessionId, string TargetMachineId, SupportSessionType ConnectionType) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote support session terminates cleanly or abnormally.
    /// </summary>
    public record RemoteSessionEnded(string SessionId, string TargetMachineId, RemoteSessionStatus Status) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote session request is approved by an administrator.
    /// </summary>
    public record RemoteSessionApproved(string SessionId, string TargetMachineId, string ApprovedByOperatorId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a remote session request is rejected or declined.
    /// </summary>
    public record RemoteSessionRejected(string SessionId, string TargetMachineId, string RejectReason) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a workstation diagnostic report is generated or collected.
    /// </summary>
    public record DiagnosticCollected(string ReportId, string MachineId, DiagnosticReportType ReportType) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a parallel chunked file transfer job starts.
    /// </summary>
    public record TransferStarted(string JobId, string FilePath, TransferDirection Direction, TransferType Category) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a transfer job successfully completes and hashes verify.
    /// </summary>
    public record TransferCompleted(string JobId, string FilePath, string FullFileIntegrityHash) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a transfer job aborts or fails block checks.
    /// </summary>
    public record TransferFailed(string JobId, string FilePath, string ErrorMessage) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a transfer job is paused.
    /// </summary>
    public record TransferPaused : Phase9BaseEvent
    {
        /// <summary>
        /// Gets or sets Job ID.
        /// </summary>
        public string JobId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets target file path.
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets status.
        /// </summary>
        public TransferStatus Status { get; init; }
    }

    /// <summary>
    /// Event triggered when a transfer job is resumed.
    /// </summary>
    public record TransferResumed : Phase9BaseEvent
    {
        /// <summary>
        /// Gets or sets Job ID.
        /// </summary>
        public string JobId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets target file path.
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets status.
        /// </summary>
        public TransferStatus Status { get; init; }
    }

    /// <summary>
    /// Event triggered when a transfer job is cancelled.
    /// </summary>
    public record TransferCancelled : Phase9BaseEvent
    {
        /// <summary>
        /// Gets or sets Job ID.
        /// </summary>
        public string JobId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets target file path.
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets status.
        /// </summary>
        public TransferStatus Status { get; init; }
    }

    /// <summary>
    /// Event triggered when a transfer job's progress is updated.
    /// </summary>
    public record TransferProgressChanged : Phase9BaseEvent
    {
        /// <summary>
        /// Gets or sets Job ID.
        /// </summary>
        public string JobId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets bytes transferred.
        /// </summary>
        public long TransferredBytes { get; init; }

        /// <summary>
        /// Gets or sets transfer speed in bytes per second.
        /// </summary>
        public double BytesPerSecSpeed { get; init; }

        /// <summary>
        /// Gets or sets ETA.
        /// </summary>
        public TimeSpan EstimatedTimeRemaining { get; init; }
    }

    /// <summary>
    /// Event triggered when a file checksum has been validated.
    /// </summary>
    public record ChecksumValidated : Phase9BaseEvent
    {
        /// <summary>
        /// Gets or sets Job ID.
        /// </summary>
        public string JobId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets target file path.
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets hash algorithm.
        /// </summary>
        public string HashAlgorithm { get; init; } = "SHA256";

        /// <summary>
        /// Gets or sets hash value.
        /// </summary>
        public string HashValue { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets whether validated.
        /// </summary>
        public bool IsValidated { get; init; }
    }

    /// <summary>
    /// Event triggered when a file integrity verification fails.
    /// </summary>
    public record IntegrityFailureDetected : Phase9BaseEvent
    {
        /// <summary>
        /// Gets or sets Job ID.
        /// </summary>
        public string JobId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets target file path.
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets expected hash.
        /// </summary>
        public string ExpectedHash { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets calculated hash.
        /// </summary>
        public string CalculatedHash { get; init; } = string.Empty;
    }

    /// <summary>
    /// Event triggered when a hardware or software asset item is discovered during system scan.
    /// </summary>
    public record AssetDiscovered(string AssetId, string MachineId, string Name, AssetType Category) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when asset inventory specifications, state, or license seats update.
    /// </summary>
    public record AssetUpdated(string AssetId, string MachineId, AssetStatus Status) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when an administrative audit log entry is successfully committed to SQLite.
    /// </summary>
    public record AuditRecordCreated(long EntryId, string CorrelationId, AuditOperationType ActionType, string OperatorId) : Phase9BaseEvent;
}
