using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Models.Phase9.Domain
{
    using CommandStatus = Sayra.Client.Shared.Models.Phase9.Enums.CommandStatus;
    #region Fleet Domain Models

    /// <summary>
    /// Value Object representing system version information.
    /// </summary>
    public record MachineVersion
    {
        /// <summary>
        /// Gets the semantic version string.
        /// </summary>
        public string SemVer { get; init; } = "1.0.0";

        /// <summary>
        /// Gets the build or commit hash.
        /// </summary>
        public string BuildHash { get; init; } = string.Empty;

        /// <summary>
        /// Gets the date when the build was generated.
        /// </summary>
        public DateTime BuildDate { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Value Object representing active hardware or software specifications.
    /// </summary>
    public record MachineInventory
    {
        /// <summary>
        /// Gets the CPU hardware description.
        /// </summary>
        public string CpuName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the GPU hardware description.
        /// </summary>
        public string GpuName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the size of RAM in Gigabytes.
        /// </summary>
        public int RamGb { get; init; }

        /// <summary>
        /// Gets the OS version and build number.
        /// </summary>
        public string OperatingSystem { get; init; } = string.Empty;

        /// <summary>
        /// Gets a collection of disk storage volume details.
        /// </summary>
        public Dictionary<string, string> StorageDrives { get; init; } = new();

        /// <summary>
        /// Determines value equality for all properties including deep dictionary comparison.
        /// </summary>
        public virtual bool Equals(MachineInventory? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            bool drivesEqual = StorageDrives.Count == other.StorageDrives.Count;
            if (drivesEqual)
            {
                foreach (var kvp in StorageDrives)
                {
                    if (!other.StorageDrives.TryGetValue(kvp.Key, out var otherVal) || kvp.Value != otherVal)
                    {
                        drivesEqual = false;
                        break;
                    }
                }
            }

            return CpuName == other.CpuName &&
                   GpuName == other.GpuName &&
                   RamGb == other.RamGb &&
                   OperatingSystem == other.OperatingSystem &&
                   drivesEqual;
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(CpuName);
            hash.Add(GpuName);
            hash.Add(RamGb);
            hash.Add(OperatingSystem);
            foreach (var kvp in StorageDrives)
            {
                hash.Add(kvp.Key);
                hash.Add(kvp.Value);
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Represents the immutable core metadata and details of a workstation machine.
    /// </summary>
    public record MachineInfo
    {
        /// <summary>
        /// Gets the unique identifier for the machine.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the hostname of the workstation.
        /// </summary>
        public string Hostname { get; init; } = string.Empty;

        /// <summary>
        /// Gets the active IP Address.
        /// </summary>
        public string IpAddress { get; init; } = string.Empty;

        /// <summary>
        /// Gets the hardware MAC Address.
        /// </summary>
        public string MacAddress { get; init; } = string.Empty;

        /// <summary>
        /// Gets the active operation status.
        /// </summary>
        public MachineStatus Status { get; init; } = MachineStatus.Offline;

        /// <summary>
        /// Gets the health score assessment.
        /// </summary>
        public MachineHealthStatus HealthStatus { get; init; } = MachineHealthStatus.Unknown;

        /// <summary>
        /// Gets the active system versions.
        /// </summary>
        public MachineVersion Version { get; init; } = new();

        /// <summary>
        /// Gets the hardware and software assets inventory snapshot.
        /// </summary>
        public MachineInventory Inventory { get; init; } = new();

        /// <summary>
        /// Gets the last seen heart-beat timestamp.
        /// </summary>
        public DateTime LastSeenUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a security/integrity state snapshot of a workstation.
    /// </summary>
    public record MachineSnapshot
    {
        /// <summary>
        /// Gets the workstation identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the timestamp when the snapshot was generated.
        /// </summary>
        public DateTime CapturedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the connection health state.
        /// </summary>
        public ConnectionStatus Connection { get; init; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// Gets compliance scoring status.
        /// </summary>
        public ComplianceStatus Compliance { get; init; } = ComplianceStatus.Evaluating;

        /// <summary>
        /// Gets active session status.
        /// </summary>
        public string ActiveSessionId { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents a dynamic or static group of fleet workstations.
    /// </summary>
    public record FleetGroup
    {
        /// <summary>
        /// Gets the group identifier.
        /// </summary>
        public string GroupId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the name of the group.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the group description.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets the classification type (Static/Dynamic).
        /// </summary>
        public FleetGroupType GroupType { get; init; } = FleetGroupType.Static;

        /// <summary>
        /// Gets the rule expression if the group is Dynamic.
        /// </summary>
        public string DynamicRuleExpression { get; init; } = string.Empty;
    }

    /// <summary>
    /// Value Object representing a search tag applied to workstations or groups.
    /// </summary>
    public record FleetTag
    {
        /// <summary>
        /// Gets the key of the tag (e.g. Room).
        /// </summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>
        /// Gets the value of the tag (e.g. VIP).
        /// </summary>
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents a regional grouping for routing rules or administrative partitioning.
    /// </summary>
    public record FleetRegion
    {
        /// <summary>
        /// Gets the region identifier.
        /// </summary>
        public string RegionId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the regional area name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the region division type.
        /// </summary>
        public FleetRegionType RegionType { get; init; } = FleetRegionType.Default;
    }

    /// <summary>
    /// Represents an organizational business division or department.
    /// </summary>
    public record FleetDepartment
    {
        /// <summary>
        /// Gets the department identifier.
        /// </summary>
        public string DepartmentId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the name of the department.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the department functional role.
        /// </summary>
        public FleetDepartmentType DepartmentType { get; init; } = FleetDepartmentType.StandardGaming;
    }

    #endregion

    #region Health & Performance Models

    /// <summary>
    /// Represents real-time mathematical health scores and active metrics.
    /// </summary>
    public record MachineHealth
    {
        /// <summary>
        /// Gets the target machine identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the evaluated health percentage score (0-100).
        /// </summary>
        public double OverallHealthScore { get; init; } = 100.0;

        /// <summary>
        /// Gets the active critical warnings count.
        /// </summary>
        public int ActiveWarningsCount { get; init; }

        /// <summary>
        /// Gets the active emergency issues count.
        /// </summary>
        public int ActiveEmergenciesCount { get; init; }

        /// <summary>
        /// Gets a set of subsystem scores.
        /// </summary>
        public Dictionary<string, double> SubsystemScores { get; init; } = new();
    }

    /// <summary>
    /// Represents a detailed subsystem and resource pressure snapshot.
    /// </summary>
    public record HealthSnapshot
    {
        /// <summary>
        /// Gets the diagnostic snapshot identifier.
        /// </summary>
        public Guid SnapshotId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Gets the timestamp of collection.
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets active CPU load percentage.
        /// </summary>
        public double CpuUtilization { get; init; }

        /// <summary>
        /// Gets memory utilization percentage.
        /// </summary>
        public double MemoryUtilization { get; init; }

        /// <summary>
        /// Gets standard storage drives remaining health.
        /// </summary>
        public double StorageUtilization { get; init; }

        /// <summary>
        /// Gets active network traffic speed in Bytes/sec.
        /// </summary>
        public double NetworkThroughputBytesPerSec { get; init; }
    }

    #endregion

    #region Remote Control & Commands Models

    /// <summary>
    /// Value Object representing a dynamic execution parameter argument.
    /// </summary>
    public record CommandParameter
    {
        /// <summary>
        /// Gets the parameter name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the parameter serialized value.
        /// </summary>
        public string Value { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether the parameter is encrypted or sensitive.
        /// </summary>
        public bool IsSecure { get; init; }
    }

    /// <summary>
    /// Represents a signed administrative remote control command definition.
    /// </summary>
    public record RemoteCommand
    {
        /// <summary>
        /// Gets the command tracking identifier.
        /// </summary>
        public string CommandId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the action descriptor verb (e.g. UNLOCK_PC, RESTART).
        /// </summary>
        public string Action { get; init; } = string.Empty;

        /// <summary>
        /// Gets the targeted client machine identifier.
        /// </summary>
        public string TargetMachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the scheduling priority level.
        /// </summary>
        public CommandPriority Priority { get; init; } = CommandPriority.Normal;

        /// <summary>
        /// Gets list of execution variables.
        /// </summary>
        public List<CommandParameter> Parameters { get; init; } = new();

        /// <summary>
        /// Gets the cryptographically chained digital signature verifying authenticity.
        /// </summary>
        public string Signature { get; init; } = string.Empty;

        /// <summary>
        /// Gets the operator identifier of the executing administrator.
        /// </summary>
        public string CreatorOperatorId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the expiration timestamp for validation windows.
        /// </summary>
        public DateTime ExpiresAtUtc { get; init; } = DateTime.UtcNow.AddMinutes(5);
    }

    /// <summary>
    /// Represents the execution outcome details of a remote command.
    /// </summary>
    public record CommandResult
    {
        /// <summary>
        /// Gets the command tracking identifier.
        /// </summary>
        public string CommandId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the target machine identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the execution status.
        /// </summary>
        public CommandStatus Status { get; init; } = CommandStatus.Pending;

        /// <summary>
        /// Gets the operation result type.
        /// </summary>
        public OperationResult Outcome { get; init; } = OperationResult.ValidationError;

        /// <summary>
        /// Gets any output logs or error messages.
        /// </summary>
        public string OutputMessage { get; init; } = string.Empty;

        /// <summary>
        /// Gets the execution duration in milliseconds.
        /// </summary>
        public long ExecutionDurationMs { get; init; }

        /// <summary>
        /// Gets the timestamp when completed.
        /// </summary>
        public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a multi-machine bulk execution context.
    /// </summary>
    public record BulkOperation
    {
        /// <summary>
        /// Gets the operation tracker identifier.
        /// </summary>
        public string BulkOperationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the action descriptor verb to execute.
        /// </summary>
        public string Action { get; init; } = string.Empty;

        /// <summary>
        /// Gets the collection of target workstation identifiers.
        /// </summary>
        public List<string> TargetMachineIds { get; init; } = new();

        /// <summary>
        /// Gets the orchestration status.
        /// </summary>
        public OperationStatus Status { get; init; } = OperationStatus.Pending;

        /// <summary>
        /// Gets the administrator who initiated the action.
        /// </summary>
        public string OperatorId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the timestamp when initialized.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Value Object representing live execution counts of a bulk operation.
    /// </summary>
    public record BulkOperationProgress
    {
        /// <summary>
        /// Gets the active status of overall operation.
        /// </summary>
        public OperationStatus ActiveStatus { get; init; } = OperationStatus.Running;

        /// <summary>
        /// Gets the number of machines targeted.
        /// </summary>
        public int TotalTargets { get; init; }

        /// <summary>
        /// Gets the number of completed tasks.
        /// </summary>
        public int CompletedCount { get; init; }

        /// <summary>
        /// Gets the number of successful tasks.
        /// </summary>
        public int SucceededCount { get; init; }

        /// <summary>
        /// Gets the number of failed tasks.
        /// </summary>
        public int FailedCount { get; init; }

        /// <summary>
        /// Gets the completion percentage (0.0 to 100.0).
        /// </summary>
        public double PercentageComplete => TotalTargets > 0 ? (double)CompletedCount / TotalTargets * 100.0 : 0.0;
    }

    /// <summary>
    /// Represents the overall completed summary outcome of a bulk operation.
    /// </summary>
    public record BulkOperationResult
    {
        /// <summary>
        /// Gets the bulk operation identifier.
        /// </summary>
        public string BulkOperationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets overall outcome execution status.
        /// </summary>
        public OperationStatus Status { get; init; } = OperationStatus.Completed;

        /// <summary>
        /// Gets individual machine command results.
        /// </summary>
        public List<CommandResult> MachineResults { get; init; } = new();

        /// <summary>
        /// Gets complete duration in milliseconds.
        /// </summary>
        public long CombinedDurationMs { get; init; }
    }

    #endregion

    #region Policy & Compliance Models

    /// <summary>
    /// Value Object pointing to a specific versioned policy asset.
    /// </summary>
    public record PolicyReference
    {
        /// <summary>
        /// Gets the unique policy template identifier.
        /// </summary>
        public string PolicyId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the specific version tag.
        /// </summary>
        public string VersionTag { get; init; } = string.Empty;

        /// <summary>
        /// Gets SHA-256 digital signature of policy content.
        /// </summary>
        public string ContentHash { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents a policy assignment mapping linking a workstation/group to a policy.
    /// </summary>
    public record PolicyAssignment
    {
        /// <summary>
        /// Gets the assignment identifier.
        /// </summary>
        public string AssignmentId { get; init; } = string.Empty;

        /// <summary>
        /// Gets target scope identifier (machine or group id).
        /// </summary>
        public string TargetId { get; init; } = string.Empty;

        /// <summary>
        /// Gets reference metadata of assigned policy.
        /// </summary>
        public PolicyReference Policy { get; init; } = new();

        /// <summary>
        /// Gets current assignment compliance state.
        /// </summary>
        public PolicyAssignmentStatus Status { get; init; } = PolicyAssignmentStatus.Pending;

        /// <summary>
        /// Gets details about any policy validation failures.
        /// </summary>
        public string FailureReason { get; init; } = string.Empty;

        /// <summary>
        /// Gets when the policy assignment was successfully applied.
        /// </summary>
        public DateTime? AppliedAtUtc { get; init; }
    }

    #endregion

    #region Asset & Software Licensing Models

    /// <summary>
    /// Represents a single physical/hardware component or software application asset item.
    /// </summary>
    public record AssetRecord
    {
        /// <summary>
        /// Gets the asset identifier.
        /// </summary>
        public string AssetId { get; init; } = string.Empty;

        /// <summary>
        /// Gets targeted machine identifier hosting the asset.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the descriptive name of the asset.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets hardware serial or software install footprint metadata.
        /// </summary>
        public string SerialOrSignature { get; init; } = string.Empty;

        /// <summary>
        /// Gets inventory asset classification category.
        /// </summary>
        public AssetType Category { get; init; } = AssetType.Software;

        /// <summary>
        /// Gets the active state.
        /// </summary>
        public AssetStatus Status { get; init; } = AssetStatus.Active;

        /// <summary>
        /// Gets metadata dictionary.
        /// </summary>
        public Dictionary<string, string> Specifications { get; init; } = new();
    }

    /// <summary>
    /// Represents software licensing and entitlement tracking.
    /// </summary>
    public record AssetLicense
    {
        /// <summary>
        /// Gets the license key or reference.
        /// </summary>
        public string LicenseId { get; init; } = string.Empty;

        /// <summary>
        /// Gets name of software/game title.
        /// </summary>
        public string SoftwareName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the total seat capacity (for floating multi-PC licenses).
        /// </summary>
        public int TotalSeats { get; init; }

        /// <summary>
        /// Gets number of active used license seats.
        /// </summary>
        public int ActiveSeatsUsed { get; init; }

        /// <summary>
        /// Gets expiry date.
        /// </summary>
        public DateTime? ExpiryUtc { get; init; }
    }

    #endregion

    #region Maintenance Schedules Models

    /// <summary>
    /// Represents a recurring or one-off maintenance task configuration window.
    /// </summary>
    public record MaintenanceWindow
    {
        /// <summary>
        /// Gets the maintenance window identifier.
        /// </summary>
        public string WindowId { get; init; } = string.Empty;

        /// <summary>
        /// Gets type of maintenance operations planned.
        /// </summary>
        public MaintenanceWindowType Category { get; init; } = MaintenanceWindowType.SystemCleanup;

        /// <summary>
        /// Gets the scheduled start time.
        /// </summary>
        public DateTime StartTimeUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the scheduled maximum duration.
        /// </summary>
        public TimeSpan Duration { get; init; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets whether the maintenance allows forceful active user session termination.
        /// </summary>
        public bool ForceSessionTermination { get; init; }
    }

    /// <summary>
    /// Represents the execution tracking record of an active maintenance task.
    /// </summary>
    public record MaintenanceSchedule
    {
        /// <summary>
        /// Gets the tracking schedule identifier.
        /// </summary>
        public string ScheduleId { get; init; } = string.Empty;

        /// <summary>
        /// Gets configuration details.
        /// </summary>
        public MaintenanceWindow Window { get; init; } = new();

        /// <summary>
        /// Gets the workstation scope filter (e.g. group, specific pc, entire fleet).
        /// </summary>
        public string ScopeFilter { get; init; } = string.Empty;

        /// <summary>
        /// Gets current operational status.
        /// </summary>
        public MaintenanceStatus State { get; init; } = MaintenanceStatus.Scheduled;

        /// <summary>
        /// Gets logs about completed steps.
        /// </summary>
        public string ExecutionSummary { get; init; } = string.Empty;
    }

    #endregion

    #region Security & Audit Administration Models

    /// <summary>
    /// Represents an immutable database audit entry capturing administrative operations.
    /// </summary>
    public record AuditEntry
    {
        /// <summary>
        /// Gets the database primary key tracker identifier.
        /// </summary>
        public long EntryId { get; init; }

        /// <summary>
        /// Gets trace or tracking identifier correlating transactions.
        /// </summary>
        public string CorrelationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the action classification type.
        /// </summary>
        public AuditOperationType ActionType { get; init; } = AuditOperationType.RemoteCommandExecution;

        /// <summary>
        /// Gets action description or SQL payload metadata.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets administrative operator identifier.
        /// </summary>
        public string OperatorId { get; init; } = string.Empty;

        /// <summary>
        /// Gets network remote IP address of the operator.
        /// </summary>
        public string ClientIpAddress { get; init; } = string.Empty;

        /// <summary>
        /// Gets completion result state.
        /// </summary>
        public AuditResult Outcome { get; init; } = AuditResult.Success;

        /// <summary>
        /// Gets timestamp of event.
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a security signed packet containing block-chained audit history entries.
    /// </summary>
    public record AuditRecord
    {
        /// <summary>
        /// Gets audit record identifier.
        /// </summary>
        public string AuditRecordId { get; init; } = string.Empty;

        /// <summary>
        /// Gets chronological list of audit entries inside this sealed block.
        /// </summary>
        public List<AuditEntry> Entries { get; init; } = new();

        /// <summary>
        /// Gets hash signature of preceding block for blockchain-style anti-tampering.
        /// </summary>
        public string ParentBlockSignature { get; init; } = string.Empty;

        /// <summary>
        /// Gets cryptographic signature of this block.
        /// </summary>
        public string Signature { get; init; } = string.Empty;
    }

    #endregion

    #region Remote Assistance & Support Models

    /// <summary>
    /// Represents participant metadata of an active desktop assistance stream.
    /// </summary>
    public record RemoteSessionParticipant
    {
        /// <summary>
        /// Gets participant identifier.
        /// </summary>
        public string ParticipantId { get; init; } = string.Empty;

        /// <summary>
        /// Gets participant friendly name.
        /// </summary>
        public string FriendlyName { get; init; } = string.Empty;

        /// <summary>
        /// Gets authorization role.
        /// </summary>
        public string Role { get; init; } = "Viewer";

        /// <summary>
        /// Gets when the participant joined session.
        /// </summary>
        public DateTime JoinedAtUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents an active or planned remote assistance desktop session.
    /// </summary>
    public record RemoteSession
    {
        /// <summary>
        /// Gets remote assistance session identifier.
        /// </summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>
        /// Gets target workstation machine identifier.
        /// </summary>
        public string TargetMachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets structural type of stream connection.
        /// </summary>
        public SupportSessionType ConnectionType { get; init; } = SupportSessionType.TerminalOnly;

        /// <summary>
        /// Gets current operational status.
        /// </summary>
        public RemoteSessionStatus Status { get; init; } = RemoteSessionStatus.Requested;

        /// <summary>
        /// Gets permissions granted for control.
        /// </summary>
        public SupportPermission AllowedPermissions { get; init; } = SupportPermission.ViewOnly;

        /// <summary>
        /// Gets metadata list of active connected operators.
        /// </summary>
        public List<RemoteSessionParticipant> Participants { get; init; } = new();

        /// <summary>
        /// Gets when the stream was initialized.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    }

    #endregion

    #region Remote Diagnostics & Package Compressions Models

    /// <summary>
    /// Represents a single logical diagnostic telemetry report document.
    /// </summary>
    public record DiagnosticReport
    {
        /// <summary>
        /// Gets the document identifier.
        /// </summary>
        public string ReportId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the workstation identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets report classification type.
        /// </summary>
        public DiagnosticReportType Category { get; init; } = DiagnosticReportType.GeneralHealth;

        /// <summary>
        /// Gets JSON payload content string.
        /// </summary>
        public string ContentJson { get; init; } = string.Empty;

        /// <summary>
        /// Gets when report was captured.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a compressed zip container carrying multiple diagnostic reports.
    /// </summary>
    public record DiagnosticPackage
    {
        /// <summary>
        /// Gets package tracking identifier.
        /// </summary>
        public string PackageId { get; init; } = string.Empty;

        /// <summary>
        /// Gets file name of generated archive (e.g. diag_001.zip).
        /// </summary>
        public string ArchiveFileName { get; init; } = string.Empty;

        /// <summary>
        /// Gets file size in bytes.
        /// </summary>
        public long SizeBytes { get; init; }

        /// <summary>
        /// Gets SHA-256 validation checksum of file contents.
        /// </summary>
        public string IntegrityHash { get; init; } = string.Empty;

        /// <summary>
        /// Gets target workstation identifier.
        /// </summary>
        public string SourceMachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets timestamp when generated.
        /// </summary>
        public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a comprehensive system report generated by the administrative api.
    /// </summary>
    public record AdministrationReport
    {
        /// <summary>
        /// Gets report document identifier.
        /// </summary>
        public string ReportId { get; init; } = string.Empty;

        /// <summary>
        /// Gets Title of the report.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets targeted scope description.
        /// </summary>
        public string ScopeDescription { get; init; } = string.Empty;

        /// <summary>
        /// Gets when generated.
        /// </summary>
        public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets JSON serialized results datasets.
        /// </summary>
        public string DataPayloadJson { get; init; } = string.Empty;
    }

    #endregion

    #region Notification & Operations Context Models

    /// <summary>
    /// Represents a system administrative logging/alert record.
    /// </summary>
    public record NotificationRecord
    {
        /// <summary>
        /// Gets unique tracking identifier.
        /// </summary>
        public string NotificationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets source component or workstation identifier generating the alert.
        /// </summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>
        /// Gets log message details.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets severity level.
        /// </summary>
        public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;

        /// <summary>
        /// Gets timestamp.
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets whether alert has been acknowledged by operator.
        /// </summary>
        public bool IsAcknowledged { get; init; }
    }

    /// <summary>
    /// Value Object representing system trace correlation context.
    /// </summary>
    public record CorrelationContext
    {
        /// <summary>
        /// Gets system Correlation ID.
        /// </summary>
        public string CorrelationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets system Trace ID.
        /// </summary>
        public string TraceId { get; init; } = string.Empty;

        /// <summary>
        /// Gets trace step context description.
        /// </summary>
        public string TraceSpanName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents overall task or thread execution context metadata.
    /// </summary>
    public record OperationContext
    {
        /// <summary>
        /// Gets system operational tracking correlation variables.
        /// </summary>
        public CorrelationContext Trace { get; init; } = new();

        /// <summary>
        /// Gets administrator operating credentials details.
        /// </summary>
        public string OperatorId { get; init; } = string.Empty;

        /// <summary>
        /// Gets timeout limit.
        /// </summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Represents execution context metadata targeting a specific command.
    /// </summary>
    public record CommandContext
    {
        /// <summary>
        /// Gets operational tracing details.
        /// </summary>
        public OperationContext BaseContext { get; init; } = new();

        /// <summary>
        /// Gets targeted workstation machine identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets security validation signature verifying parameters.
        /// </summary>
        public string ParameterSignature { get; init; } = string.Empty;
    }

    #endregion

    #region Secure General File Transfer Models

    /// <summary>
    /// Value Object representing a single binary slice or block of a transferring file.
    /// </summary>
    public record TransferChunk
    {
        /// <summary>
        /// Gets unique index block number.
        /// </summary>
        public int ChunkIndex { get; init; }

        /// <summary>
        /// Gets physical block size in bytes.
        /// </summary>
        public int ChunkSizeBytes { get; init; }

        /// <summary>
        /// Gets MD5 or SHA-256 block checksum signature.
        /// </summary>
        public string Checksum { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents a persistent transaction-safe binary streaming transfer task.
    /// </summary>
    public record TransferJob
    {
        /// <summary>
        /// Gets file transfer job tracking identifier.
        /// </summary>
        public string JobId { get; init; } = string.Empty;

        /// <summary>
        /// Gets source or target absolute file path.
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets transfer direction (Upload/Download).
        /// </summary>
        public TransferDirection Direction { get; init; } = TransferDirection.Download;

        /// <summary>
        /// Gets functional role of data payload.
        /// </summary>
        public TransferType Category { get; init; } = TransferType.File;

        /// <summary>
        /// Gets active operational status.
        /// </summary>
        public TransferStatus Status { get; init; } = TransferStatus.Pending;

        /// <summary>
        /// Gets total file capacity size in bytes.
        /// </summary>
        public long TotalFileSizeBytes { get; init; }

        /// <summary>
        /// Gets collection list of chunking plans.
        /// </summary>
        public List<TransferChunk> Chunks { get; init; } = new();

        /// <summary>
        /// Gets SHA-256 checksum of complete assembled binary.
        /// </summary>
        public string FullFileIntegrityHash { get; init; } = string.Empty;

        /// <summary>
        /// Gets when transfer initiated.
        /// </summary>
        public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Value Object representing live speed metrics of a file transfer.
    /// </summary>
    public record TransferProgress
    {
        /// <summary>
        /// Gets active job tracking identifier.
        /// </summary>
        public string JobId { get; init; } = string.Empty;

        /// <summary>
        /// Gets completed downloaded/uploaded bytes count.
        /// </summary>
        public long TransferredBytes { get; init; }

        /// <summary>
        /// Gets EMA-smoothed transfer speed in Bytes/sec.
        /// </summary>
        public double BytesPerSecSpeed { get; init; }

        /// <summary>
        /// Gets estimated duration to completion.
        /// </summary>
        public TimeSpan EstimatedTimeRemaining { get; init; } = TimeSpan.Zero;
    }

    #endregion

    #region Configuration Snapshot Model

    /// <summary>
    /// Represents a point-in-time configuration state snapshot.
    /// </summary>
    public record ConfigurationSnapshot
    {
        /// <summary>
        /// Gets the configuration version.
        /// </summary>
        public string ConfigVersion { get; init; } = string.Empty;

        /// <summary>
        /// Gets configuration timestamp.
        /// </summary>
        public DateTime AppliedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets options map properties list.
        /// </summary>
        public Dictionary<string, string> SettingsMap { get; init; } = new();
    }

    #endregion

    #region Remote File Management Stage 6 Models

    /// <summary>
    /// Represents a file entry in a directory listing.
    /// </summary>
    public record FileEntry
    {
        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the absolute path to the file.
        /// </summary>
        public string FullPath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the size of the file in bytes.
        /// </summary>
        public long SizeBytes { get; init; }

        /// <summary>
        /// Gets the last write time of the file in UTC.
        /// </summary>
        public DateTime LastWriteTimeUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets whether the file is read-only.
        /// </summary>
        public bool IsReadOnly { get; init; }
    }

    /// <summary>
    /// Represents detailed metadata for a file.
    /// </summary>
    public record FileMetadata
    {
        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the absolute path to the file.
        /// </summary>
        public string FullPath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the size of the file in bytes.
        /// </summary>
        public long SizeBytes { get; init; }

        /// <summary>
        /// Gets the creation time of the file in UTC.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the last write time of the file in UTC.
        /// </summary>
        public DateTime LastWriteTimeUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the last access time of the file in UTC.
        /// </summary>
        public DateTime LastAccessTimeUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the SHA-256 integrity hash value of the file content.
        /// </summary>
        public string ChecksumSha256 { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether the file is read-only.
        /// </summary>
        public bool IsReadOnly { get; init; }

        /// <summary>
        /// Gets custom metadata properties or attributes associated with the file.
        /// </summary>
        public Dictionary<string, string> Attributes { get; init; } = new();
    }

    /// <summary>
    /// Represents a directory entry in a directory listing.
    /// </summary>
    public record DirectoryEntry
    {
        /// <summary>
        /// Gets the name of the directory.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the absolute path of the directory.
        /// </summary>
        public string FullPath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the last write time of the directory in UTC.
        /// </summary>
        public DateTime LastWriteTimeUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the list of subdirectories inside this directory.
        /// </summary>
        public List<DirectoryEntry> SubDirectories { get; init; } = new();

        /// <summary>
        /// Gets the list of files inside this directory.
        /// </summary>
        public List<FileEntry> Files { get; init; } = new();
    }

    /// <summary>
    /// Represents the result of a completed or failed file transfer job.
    /// </summary>
    public record TransferResult
    {
        /// <summary>
        /// Gets the unique job identifier.
        /// </summary>
        public string JobId { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether the transfer completed successfully.
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// Gets the path of the transferred file.
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the final integrity hash of the transferred file.
        /// </summary>
        public string FullFileIntegrityHash { get; init; } = string.Empty;

        /// <summary>
        /// Gets the total number of bytes transferred.
        /// </summary>
        public long TransferredBytes { get; init; }

        /// <summary>
        /// Gets the total duration of the transfer.
        /// </summary>
        public TimeSpan Duration { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the error message if the transfer failed.
        /// </summary>
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Represents cumulative transfer statistics for monitoring.
    /// </summary>
    public record TransferStatistics
    {
        /// <summary>
        /// Gets the total number of transfer jobs executed.
        /// </summary>
        public int TotalJobs { get; init; }

        /// <summary>
        /// Gets the number of successful transfer jobs.
        /// </summary>
        public int SucceededJobs { get; init; }

        /// <summary>
        /// Gets the number of failed transfer jobs.
        /// </summary>
        public int FailedJobs { get; init; }

        /// <summary>
        /// Gets the total bytes transferred across all jobs.
        /// </summary>
        public long TotalBytesTransferred { get; init; }

        /// <summary>
        /// Gets the average transfer speed in bytes per second.
        /// </summary>
        public double AverageSpeedBytesPerSec { get; init; }

        /// <summary>
        /// Gets the total cumulative duration of all transfers.
        /// </summary>
        public TimeSpan TotalDuration { get; init; } = TimeSpan.Zero;
    }

    /// <summary>
    /// Represents cryptographic checksum validation information.
    /// </summary>
    public record ChecksumInfo
    {
        /// <summary>
        /// Gets the cryptographic hashing algorithm used (e.g., SHA256, SHA512).
        /// </summary>
        public string HashAlgorithm { get; init; } = string.Empty;

        /// <summary>
        /// Gets the calculated hash hex string.
        /// </summary>
        public string HashValue { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether the checksum was successfully validated.
        /// </summary>
        public bool IsValidated { get; init; }

        /// <summary>
        /// Gets the timestamp of when the validation occurred in UTC.
        /// </summary>
        public DateTime ValidatedAtUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents error details for a failed transfer job.
    /// </summary>
    public record TransferError
    {
        /// <summary>
        /// Gets the categorized error code.
        /// </summary>
        public string ErrorCode { get; init; } = string.Empty;

        /// <summary>
        /// Gets the error message description.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets the UTC timestamp when the error occurred.
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the stack trace details if available.
        /// </summary>
        public string? StackTrace { get; init; }
    }

    /// <summary>
    /// Represents a live session coordinating active transfer streams.
    /// </summary>
    public record TransferSession
    {
        /// <summary>
        /// Gets the unique session identifier.
        /// </summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the active job identifier.
        /// </summary>
        public string JobId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the UTC timestamp of when the session was initialized.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the UTC timestamp of when the session was last active.
        /// </summary>
        public DateTime LastActiveAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the current active status of the transfer.
        /// </summary>
        public TransferStatus Status { get; init; } = TransferStatus.Pending;
    }

    #endregion
}
