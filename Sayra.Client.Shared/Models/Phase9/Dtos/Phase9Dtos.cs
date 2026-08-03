using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Models.Phase9.Dtos
{
    /// <summary>
    /// DTO for querying workstations with dynamic filters.
    /// </summary>
    public class MachineQueryRequest
    {
        /// <summary>
        /// Gets or sets target workstation IDs to filter.
        /// </summary>
        public List<string> MachineIds { get; set; } = new();

        /// <summary>
        /// Gets or sets dynamic Status filter.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets dynamic HealthStatus filter.
        /// </summary>
        public string? HealthStatus { get; set; }

        /// <summary>
        /// Gets or sets minimum RAM size filter.
        /// </summary>
        public int? MinRamGb { get; set; }
    }

    /// <summary>
    /// DTO for querying fleet group allocations and metadata.
    /// </summary>
    public class FleetQueryRequest
    {
        /// <summary>
        /// Gets or sets group type filter (Static or Dynamic).
        /// </summary>
        public string? GroupType { get; set; }

        /// <summary>
        /// Gets or sets search tag keyword.
        /// </summary>
        public string? SearchKeyword { get; set; }
    }

    /// <summary>
    /// DTO representing an incoming remote administrative command request.
    /// </summary>
    public class RemoteCommandRequest
    {
        /// <summary>
        /// Gets or sets the target machine identifier.
        /// </summary>
        public string MachineId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command action (e.g. LOCK, RESTART).
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the scheduling priority level.
        /// </summary>
        public string Priority { get; set; } = "Normal";

        /// <summary>
        /// Gets or sets dynamic parameter arguments list.
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; } = new();

        /// <summary>
        /// Gets or sets the cryptographic signature verifying author authenticity.
        /// </summary>
        public string Signature { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the administrator operator identifier.
        /// </summary>
        public string OperatorId { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO representing a remote command dispatch/execution response.
    /// </summary>
    public class RemoteCommandResponse
    {
        /// <summary>
        /// Gets or sets command tracking ID.
        /// </summary>
        public string CommandId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets current execution status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets completion outcome.
        /// </summary>
        public string Outcome { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets output message logs or errors.
        /// </summary>
        public string OutputMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for initiating a multi-machine bulk execution operation.
    /// </summary>
    public class BulkOperationRequest
    {
        /// <summary>
        /// Gets or sets action verb.
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets targeted workstation IDs.
        /// </summary>
        public List<string> MachineIds { get; set; } = new();

        /// <summary>
        /// Gets or sets target group IDs (optional).
        /// </summary>
        public List<string> GroupIds { get; set; } = new();

        /// <summary>
        /// Gets or sets the administrator operator identifier.
        /// </summary>
        public string OperatorId { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO capturing bulk operation execution summary feedback.
    /// </summary>
    public class BulkOperationResponse
    {
        /// <summary>
        /// Gets or sets bulk tracking ID.
        /// </summary>
        public string BulkOperationId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets operation status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets total target counts.
        /// </summary>
        public int TotalTargets { get; set; }

        /// <summary>
        /// Gets or sets completed workstation targets count.
        /// </summary>
        public int SucceededCount { get; set; }

        /// <summary>
        /// Gets or sets failed workstation targets count.
        /// </summary>
        public int FailedCount { get; set; }
    }

    /// <summary>
    /// DTO for assigning a security policy template to target workstations or groups.
    /// </summary>
    public class PolicyAssignmentRequest
    {
        /// <summary>
        /// Gets or sets central policy ID.
        /// </summary>
        public string PolicyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets version tag string.
        /// </summary>
        public string VersionTag { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets target scope ID (machine or group).
        /// </summary>
        public string TargetId { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for scheduling or triggering a maintenance execution window.
    /// </summary>
    public class MaintenanceRequest
    {
        /// <summary>
        /// Gets or sets maintenance category type.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets scope filter keyword.
        /// </summary>
        public string ScopeFilter { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets scheduled start date.
        /// </summary>
        public DateTime StartTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets duration in minutes.
        /// </summary>
        public int DurationMinutes { get; set; }
    }

    /// <summary>
    /// DTO for commanding a workstation to perform diagnostics checks.
    /// </summary>
    public class DiagnosticRequest
    {
        /// <summary>
        /// Gets or sets target machine ID.
        /// </summary>
        public string MachineId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets diagnostics report category type.
        /// </summary>
        public string ReportType { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for initiating a segmented secure transfer transaction job.
    /// </summary>
    public class TransferRequest
    {
        /// <summary>
        /// Gets or sets local file path.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets direction (Upload/Download).
        /// </summary>
        public string Direction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets binary functional role.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets total file size.
        /// </summary>
        public long TotalFileSizeBytes { get; set; }
    }

    /// <summary>
    /// DTO capturing transfer status feedback.
    /// </summary>
    public class TransferResponse
    {
        /// <summary>
        /// Gets or sets job tracking ID.
        /// </summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets active status state.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets total chunks count.
        /// </summary>
        public int TotalChunks { get; set; }

        /// <summary>
        /// Gets or sets successfully written chunks count.
        /// </summary>
        public int CompletedChunks { get; set; }
    }

    /// <summary>
    /// DTO for requesting or establishing a remote assistance support stream connection.
    /// </summary>
    public class RemoteSupportRequest
    {
        /// <summary>
        /// Gets or sets target machine ID.
        /// </summary>
        public string MachineId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets visual protocol type (TerminalOnly, DesktopStreaming, UnifiedRemoteSupport).
        /// </summary>
        public string SessionType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets requested interactive control rights.
        /// </summary>
        public string RequestedPermission { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for querying historical database admin audit logs.
    /// </summary>
    public class AuditQueryRequest
    {
        /// <summary>
        /// Gets or sets administrator operator ID.
        /// </summary>
        public string? OperatorId { get; set; }

        /// <summary>
        /// Gets or sets filter start date.
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Gets or sets filter end date.
        /// </summary>
        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// DTO for generating aggregate administration reports.
    /// </summary>
    public class AdministrationReportRequest
    {
        /// <summary>
        /// Gets or sets report title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets query scope filter constraints.
        /// </summary>
        public string ScopeFilter { get; set; } = string.Empty;
    }
}
