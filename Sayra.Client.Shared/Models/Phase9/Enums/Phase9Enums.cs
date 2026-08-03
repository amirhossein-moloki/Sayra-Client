using System;

namespace Sayra.Client.Shared.Models.Phase9.Enums
{
    /// <summary>
    /// Represents the operational status of a workstation machine in the fleet.
    /// </summary>
    public enum MachineStatus
    {
        /// <summary>
        /// Machine is offline or unreachable.
        /// </summary>
        Offline,

        /// <summary>
        /// Machine is online, registered, and idle.
        /// </summary>
        Online,

        /// <summary>
        /// Machine is currently occupied in a user session.
        /// </summary>
        InSession,

        /// <summary>
        /// Machine is locked out by administration.
        /// </summary>
        Locked,

        /// <summary>
        /// Machine is in maintenance mode.
        /// </summary>
        Maintenance,

        /// <summary>
        /// Machine is shutting down or rebooting.
        /// </summary>
        Transitioning
    }

    /// <summary>
    /// Represents the comprehensive health evaluation status of a machine.
    /// </summary>
    public enum MachineHealthStatus
    {
        /// <summary>
        /// Machine has no detected issues.
        /// </summary>
        Healthy,

        /// <summary>
        /// Machine has non-critical warnings.
        /// </summary>
        Warning,

        /// <summary>
        /// Machine is experiencing critical performance or hardware anomalies.
        /// </summary>
        Critical,

        /// <summary>
        /// Machine is in an emergency state requiring immediate self-healing or administrative intervention.
        /// </summary>
        Emergency,

        /// <summary>
        /// Health status is unknown (e.g. offline).
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Represents the classification type of a fleet workstation group.
    /// </summary>
    public enum FleetGroupType
    {
        /// <summary>
        /// Statistically or manually assigned workstation membership.
        /// </summary>
        Static,

        /// <summary>
        /// Dynamic membership evaluated based on metadata and rules.
        /// </summary>
        Dynamic,

        /// <summary>
        /// Special transient or system group.
        /// </summary>
        System
    }

    /// <summary>
    /// Represents the region categorization for regional fleet routing or organizational rules.
    /// </summary>
    public enum FleetRegionType
    {
        /// <summary>
        /// Primary default region.
        /// </summary>
        Default,

        /// <summary>
        /// Local area network or site-specific division.
        /// </summary>
        Local,

        /// <summary>
        /// Regional center, franchise division, or zone.
        /// </summary>
        Regional,

        /// <summary>
        /// Global partition.
        /// </summary>
        Global
    }

    /// <summary>
    /// Represents the department division or cost center of a group of workstations.
    /// </summary>
    public enum FleetDepartmentType
    {
        /// <summary>
        /// Standard gaming floor workstations.
        /// </summary>
        StandardGaming,

        /// <summary>
        /// High-end or VIP sector gaming stations.
        /// </summary>
        VipGaming,

        /// <summary>
        /// Administrative computers, cash registers, or operator terminals.
        /// </summary>
        Administration,

        /// <summary>
        /// Dedicated tournament or staging setups.
        /// </summary>
        Tournament,

        /// <summary>
        /// Other specialized workstation roles.
        /// </summary>
        Specialized
    }

    /// <summary>
    /// Represents the priority weighting of a remote control command.
    /// </summary>
    public enum CommandPriority
    {
        /// <summary>
        /// Low priority, executed sequentially when system is idle.
        /// </summary>
        Low,

        /// <summary>
        /// Standard operational priority.
        /// </summary>
        Normal,

        /// <summary>
        /// High priority, prioritizes in front of standard command queue.
        /// </summary>
        High,

        /// <summary>
        /// Immediate execution, bypasses queue logic and interrupts non-critical processes.
        /// </summary>
        Critical,

        /// <summary>
        /// Maximum priority emergency lockdown or rescue command.
        /// </summary>
        Emergency
    }

    /// <summary>
    /// Represents the lifecycle state of a single remote command execution.
    /// </summary>
    public enum CommandStatus
    {
        /// <summary>
        /// Command is created and awaiting dispatch.
        /// </summary>
        Pending,

        /// <summary>
        /// Command has been sent to the target workstation.
        /// </summary>
        Dispatched,

        /// <summary>
        /// Command has been received and is currently executing.
        /// </summary>
        Executing,

        /// <summary>
        /// Command completed successfully.
        /// </summary>
        Succeeded,

        /// <summary>
        /// Command failed to execute successfully.
        /// </summary>
        Failed,

        /// <summary>
        /// Command execution was cancelled or aborted.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Command expired before dispatch or execution finished.
        /// </summary>
        Expired,

        /// <summary>
        /// Command was routed to the dead-letter queue.
        /// </summary>
        DeadLettered
    }

    /// <summary>
    /// Represents the status of a bulk multi-machine orchestration operation.
    /// </summary>
    public enum OperationStatus
    {
        /// <summary>
        /// Bulk operation is initialized and waiting to start.
        /// </summary>
        Pending,

        /// <summary>
        /// Bulk operation is currently executing tasks on target machines.
        /// </summary>
        Running,

        /// <summary>
        /// Bulk operation has completed on all targets.
        /// </summary>
        Completed,

        /// <summary>
        /// Bulk operation was aborted or cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Bulk operation failed overall or was aborted due to high failure thresholds.
        /// </summary>
        Failed,

        /// <summary>
        /// Bulk operation completed with a mixture of successes and failures.
        /// </summary>
        PartiallySucceeded
    }

    /// <summary>
    /// Represents the individual outcome result of an operation execution.
    /// </summary>
    public enum OperationResult
    {
        /// <summary>
        /// Operation was successful.
        /// </summary>
        Success,

        /// <summary>
        /// Operation failed with a handled error.
        /// </summary>
        Failure,

        /// <summary>
        /// Operation was bypassed due to preconditions.
        /// </summary>
        Skipped,

        /// <summary>
        /// Operation timed out during execution.
        /// </summary>
        Timeout,

        /// <summary>
        /// Operation failed validation checks.
        /// </summary>
        ValidationError,

        /// <summary>
        /// Operation failed security validation.
        /// </summary>
        SecurityError
    }

    /// <summary>
    /// Represents the progress state of a remote file transfer job.
    /// </summary>
    public enum TransferStatus
    {
        /// <summary>
        /// Job is registered and in queue.
        /// </summary>
        Pending,

        /// <summary>
        /// Files are being prepared, calculated, or hashed.
        /// </summary>
        Preparing,

        /// <summary>
        /// Data blocks/chunks are actively transferring.
        /// </summary>
        Transferring,

        /// <summary>
        /// Transfer is temporarily paused.
        /// </summary>
        Paused,

        /// <summary>
        /// Transfer completed successfully and integrity was validated.
        /// </summary>
        Completed,

        /// <summary>
        /// Transfer failed due to networking, storage, or integrity failure.
        /// </summary>
        Failed,

        /// <summary>
        /// Transfer was cancelled.
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// Represents the flow direction of a file transfer.
    /// </summary>
    public enum TransferDirection
    {
        /// <summary>
        /// File upload from target client machine to central server/storage.
        /// </summary>
        Upload,

        /// <summary>
        /// File download from central server/repository to client machine.
        /// </summary>
        Download
    }

    /// <summary>
    /// Represents the protocol/content type of data being transferred.
    /// </summary>
    public enum TransferType
    {
        /// <summary>
        /// Standard binary file or library component.
        /// </summary>
        File,

        /// <summary>
        /// Update package binary stream.
        /// </summary>
        UpdatePackage,

        /// <summary>
        /// Diagnostic reports bundle.
        /// </summary>
        DiagnosticBundle,

        /// <summary>
        /// System configuration, database, or policy file.
        /// </summary>
        Configuration,

        /// <summary>
        /// Advertisement or media content.
        /// </summary>
        MediaAsset
    }

    /// <summary>
    /// Represents the status of a corporate workstation security policy definition.
    /// </summary>
    public enum PolicyStatus
    {
        /// <summary>
        /// Policy is in draft state.
        /// </summary>
        Draft,

        /// <summary>
        /// Policy is active and approved for deployment.
        /// </summary>
        Active,

        /// <summary>
        /// Policy has been deactivated or retired.
        /// </summary>
        Deprecated,

        /// <summary>
        /// Policy is archived.
        /// </summary>
        Archived
    }

    /// <summary>
    /// Represents the assignment application state of a policy on a machine or group.
    /// </summary>
    public enum PolicyAssignmentStatus
    {
        /// <summary>
        /// Policy assignment is planned but not applied.
        /// </summary>
        Pending,

        /// <summary>
        /// Policy is actively being pushed and configured.
        /// </summary>
        Applying,

        /// <summary>
        /// Policy was successfully applied and is active.
        /// </summary>
        Applied,

        /// <summary>
        /// Policy application failed on target.
        /// </summary>
        Failed,

        /// <summary>
        /// Policy assignment has been removed or rolled back.
        /// </summary>
        Removed
    }

    /// <summary>
    /// Represents the active state of a scheduled maintenance window execution.
    /// </summary>
    public enum MaintenanceStatus
    {
        /// <summary>
        /// Scheduled maintenance is pending.
        /// </summary>
        Scheduled,

        /// <summary>
        /// Machine is transitioning into maintenance mode.
        /// </summary>
        Initializing,

        /// <summary>
        /// Maintenance is currently actively running.
        /// </summary>
        Running,

        /// <summary>
        /// Maintenance window finished successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// Maintenance window was skipped.
        /// </summary>
        Skipped,

        /// <summary>
        /// Maintenance task execution failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Maintenance window was cancelled.
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// Represents the category of maintenance tasks to be executed.
    /// </summary>
    public enum MaintenanceWindowType
    {
        /// <summary>
        /// General cleanup, temporary logs deletion, and system re-indexing.
        /// </summary>
        SystemCleanup,

        /// <summary>
        /// Game updates downloading and installation orchestration.
        /// </summary>
        GameUpdates,

        /// <summary>
        /// Client software or OS security updates.
        /// </summary>
        SoftwareUpgrades,

        /// <summary>
        /// Deep system hardware/software diagnostics run.
        /// </summary>
        Diagnostics,

        /// <summary>
        /// Scheduled complete machine restart or system health cycling.
        /// </summary>
        ScheduledReboot,

        /// <summary>
        /// Emergency or ad-hoc custom maintenance operations.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Represents the inventory division classification of a machine asset.
    /// </summary>
    public enum AssetType
    {
        /// <summary>
        /// Main central processing unit hardware.
        /// </summary>
        Cpu,

        /// <summary>
        /// Graphics processing unit card or hardware.
        /// </summary>
        Gpu,

        /// <summary>
        /// System volatile random-access memory module.
        /// </summary>
        Ram,

        /// <summary>
        /// Non-volatile storage drive (HDD/SSD).
        /// </summary>
        StorageDevice,

        /// <summary>
        /// Computer motherboard or system BIOS.
        /// </summary>
        Motherboard,

        /// <summary>
        /// Network interface card or device.
        /// </summary>
        NetworkAdapter,

        /// <summary>
        /// Installed software game or application.
        /// </summary>
        Software,

        /// <summary>
        /// Operating system, build versions, and major registry configurations.
        /// </summary>
        OperatingSystem,

        /// <summary>
        /// Attached peripheral hardware (e.g. mouse, keyboard, virtual audio devices).
        /// </summary>
        Peripheral,

        /// <summary>
        /// Software license certificate or registry entitlement.
        /// </summary>
        License
    }

    /// <summary>
    /// Represents the status of an inventoried hardware or software asset.
    /// </summary>
    public enum AssetStatus
    {
        /// <summary>
        /// Asset is active, functional, and fully registered.
        /// </summary>
        Active,

        /// <summary>
        /// Asset has been deprecated, uninstalled, or disconnected.
        /// </summary>
        Inactive,

        /// <summary>
        /// Asset is reporting hardware error, corrupt installation, or missing.
        /// </summary>
        Degraded,

        /// <summary>
        /// Software or license asset requires updates or activation.
        /// </summary>
        UpdateRequired,

        /// <summary>
        /// Asset has been blacklisted or failed security compliance checks.
        /// </summary>
        Blacklisted
    }

    /// <summary>
    /// Represents the lifecycle status of an active remote support assistance session.
    /// </summary>
    public enum RemoteSessionStatus
    {
        /// <summary>
        /// Support session requested by workstation.
        /// </summary>
        Requested,

        /// <summary>
        /// Support session approved by administrator, awaiting workstation handshake.
        /// </summary>
        Approved,

        /// <summary>
        /// Support session rejected or declined.
        /// </summary>
        Rejected,

        /// <summary>
        /// Support session is actively streaming telemetry and command control.
        /// </summary>
        Active,

        /// <summary>
        /// Support session is temporarily suspended.
        /// </summary>
        Paused,

        /// <summary>
        /// Support session was ended cleanly.
        /// </summary>
        Ended,

        /// <summary>
        /// Support session timed out or connection was aborted unexpectedly.
        /// </summary>
        Disconnected
    }

    /// <summary>
    /// Represents the permissions granted to an administrator during a remote assistance session.
    /// </summary>
    public enum SupportPermission
    {
        /// <summary>
        /// Read-only monitor viewing permission.
        /// </summary>
        ViewOnly,

        /// <summary>
        /// View screen and perform remote shell file/cmd execution.
        /// </summary>
        InteractiveExecution,

        /// <summary>
        /// Full interactive control including keyboard, mouse, and registry/system policies modification.
        /// </summary>
        FullControl,

        /// <summary>
        /// File transfer permissions only.
        /// </summary>
        FileTransferOnly,

        /// <summary>
        /// Temporary administrative override and session rescue controls.
        /// </summary>
        EmergencyOverride
    }

    /// <summary>
    /// Represents the protocol or transport type utilized for remote support session streaming.
    /// </summary>
    public enum SupportSessionType
    {
        /// <summary>
        /// Terminal/CLI shell secure execution stream.
        /// </summary>
        TerminalOnly,

        /// <summary>
        /// High-performance visual UI desktop stream (H.264, VP9, or custom JPEG compression sequence).
        /// </summary>
        DesktopStreaming,

        /// <summary>
        /// Process and service diagnostic logs real-time stream.
        /// </summary>
        DiagnosticsOnly,

        /// <summary>
        /// Unified desktop streaming, CLI execution, and processes management.
        /// </summary>
        UnifiedRemoteSupport
    }

    /// <summary>
    /// Represents the specialized classification of diagnostic reports generated.
    /// </summary>
    public enum DiagnosticReportType
    {
        /// <summary>
        /// General workstation health score and operational metrics.
        /// </summary>
        GeneralHealth,

        /// <summary>
        /// Thorough CPU/RAM/Drive performance tracking and bottle-neck details.
        /// </summary>
        Performance,

        /// <summary>
        /// Kernel crash, process dump, and crash recovery event logs telemetry.
        /// </summary>
        CrashDumpAnalysis,

        /// <summary>
        /// SQLite and SQLCipher database integrity and vacuum diagnostics.
        /// </summary>
        DatabaseIntegrity,

        /// <summary>
        /// Anti-tamper, security hardening validation, and policy compliance verification.
        /// </summary>
        SecurityAudit,

        /// <summary>
        /// Installed game assets, manifest checksum, and launch validation reports.
        /// </summary>
        GameLibraryHealth,

        /// <summary>
        /// Active plugins and background services status report.
        /// </summary>
        PluginsAndServices,

        /// <summary>
        /// Network latency, mirror selector speeds, and DNS diagnostic validation.
        /// </summary>
        NetworkPerformance,

        /// <summary>
        /// Drive storage allocation, cache statistics, and quota usage analytics.
        /// </summary>
        StorageAllocation
    }

    /// <summary>
    /// Represents the classification type of an administrative audit operation.
    /// </summary>
    public enum AuditOperationType
    {
        /// <summary>
        /// Direct single remote command execution.
        /// </summary>
        RemoteCommandExecution,

        /// <summary>
        /// Multi-machine parallel bulk operation.
        /// </summary>
        BulkOperation,

        /// <summary>
        /// Policy definition modification or assignment push.
        /// </summary>
        PolicyChange,

        /// <summary>
        /// Maintenance window trigger or updates execution.
        /// </summary>
        MaintenanceExecution,

        /// <summary>
        /// Security settings, trust salt, or credentials update.
        /// </summary>
        SecurityHardeningChange,

        /// <summary>
        /// Live Remote Support desktop session initialization.
        /// </summary>
        RemoteSupportSession,

        /// <summary>
        /// Database configuration change, manual vacuum, or backup/restore operations.
        /// </summary>
        DatabaseAdministration,

        /// <summary>
        /// Fleet configuration settings changed at high level.
        /// </summary>
        FleetConfiguration
    }

    /// <summary>
    /// Represents the execution outcome score of an administrative audit record.
    /// </summary>
    public enum AuditResult
    {
        /// <summary>
        /// Audit operation succeeded completely.
        /// </summary>
        Success,

        /// <summary>
        /// Audit operation failed during execution.
        /// </summary>
        Failure,

        /// <summary>
        /// Audit operation failed authorization, signature, or token validation checks.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Audit operation was aborted or cancelled by operator or timeout limits.
        /// </summary>
        Aborted
    }

    /// <summary>
    /// Represents the severity of a system notification sent to administrative dashboard.
    /// </summary>
    public enum NotificationSeverity
    {
        /// <summary>
        /// Informational event.
        /// </summary>
        Info,

        /// <summary>
        /// Warning alert, requiring potential future evaluation.
        /// </summary>
        Warning,

        /// <summary>
        /// Critical error event, which may impact system execution.
        /// </summary>
        Critical,

        /// <summary>
        /// Severe security breach, hardware failure, or service deadlock needing instant action.
        /// </summary>
        Emergency
    }

    /// <summary>
    /// Represents the network connection health between client and central server/gateway.
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>
        /// Network connection is disconnected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// Handshake and network negotiation are actively running.
        /// </summary>
        Connecting,

        /// <summary>
        /// Successfully authenticated and ready for bidirectional message passing.
        /// </summary>
        Connected,

        /// <summary>
        /// Authenticated but experiencing high latency or jitter packet losses.
        /// </summary>
        Degraded
    }

    /// <summary>
    /// Represents the offline queue database synchronisation progress with server.
    /// </summary>
    public enum SynchronizationStatus
    {
        /// <summary>
        /// Synchronization has not started or client is offline.
        /// </summary>
        Idle,

        /// <summary>
        /// Comparing transaction catalogs, sequence hashes, and offsets.
        /// </summary>
        Analyzing,

        /// <summary>
        /// Actively uploading pending offline events or downloading server actions.
        /// </summary>
        Synchronizing,

        /// <summary>
        /// Fleet workstation data is 100% matched and synced.
        /// </summary>
        Synced,

        /// <summary>
        /// Sync process encountered errors, database corruption, or key mismatches.
        /// </summary>
        Failed
    }

    /// <summary>
    /// Represents the general policy and compliance assessment score for the target machine.
    /// </summary>
    public enum ComplianceStatus
    {
        /// <summary>
        /// Workstation is 100% compliant with all assigned registry, software, and anti-tamper policies.
        /// </summary>
        Compliant,

        /// <summary>
        /// Workstation has slight mismatches (e.g. pending minor updates or reboot required).
        /// </summary>
        NonCompliantWarning,

        /// <summary>
        /// Workstation fails key security parameters, missing core software, or unauthorized registry shifts.
        /// </summary>
        ViolatedCritical,

        /// <summary>
        /// Compliance evaluation process is currently running or pending data collection.
        /// </summary>
        Evaluating
    }
}
