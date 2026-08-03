using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Interfaces.Phase9
{
    /// <summary>
    /// Service for managing workstations fleet registration, groups, and dynamic memberships.
    /// </summary>
    public interface IFleetManager
    {
        /// <summary>
        /// Registers or updates a workstation in the fleet database.
        /// </summary>
        Task<bool> RegisterMachineAsync(MachineInfo machine, CancellationToken ct = default);

        /// <summary>
        /// Removes a workstation from the active fleet.
        /// </summary>
        Task<bool> RemoveMachineAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a specific machine's detailed record.
        /// </summary>
        Task<MachineInfo?> GetMachineAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Gets all workstations currently registered in the fleet.
        /// </summary>
        Task<IReadOnlyList<MachineInfo>> GetAllMachinesAsync(CancellationToken ct = default);

        /// <summary>
        /// Creates a new workstation group (Static or Dynamic).
        /// </summary>
        Task<bool> CreateGroupAsync(FleetGroup group, CancellationToken ct = default);

        /// <summary>
        /// Deletes an existing workstation group.
        /// </summary>
        Task<bool> DeleteGroupAsync(string groupId, CancellationToken ct = default);

        /// <summary>
        /// Assigns a static machine to a fleet group.
        /// </summary>
        Task<bool> AssignMachineToGroupAsync(string machineId, string groupId, CancellationToken ct = default);

        /// <summary>
        /// Removes a static machine assignment from a fleet group.
        /// </summary>
        Task<bool> RemoveMachineFromGroupAsync(string machineId, string groupId, CancellationToken ct = default);

        /// <summary>
        /// Gets all active workstations associated with a specific fleet group.
        /// </summary>
        Task<IReadOnlyList<MachineInfo>> GetGroupMembersAsync(string groupId, CancellationToken ct = default);
    }

    /// <summary>
    /// Core operational service handling execution of individual remote control commands on workstations.
    /// </summary>
    public interface IRemoteCommandService
    {
        /// <summary>
        /// Invokes a remote command on a single target machine.
        /// </summary>
        Task<CommandResult> ExecuteCommandAsync(RemoteCommand command, CancellationToken ct = default);
    }

    /// <summary>
    /// Service responsible for dispatching, routing, and tracking commands to target endpoints.
    /// </summary>
    public interface IRemoteCommandDispatcher
    {
        /// <summary>
        /// Dispatches a command to the remote transport layer.
        /// </summary>
        Task<bool> DispatchCommandAsync(RemoteCommand command, CancellationToken ct = default);
    }

    /// <summary>
    /// Thread-safe queuing coordinator for managing delayed, offline, or priority commands.
    /// </summary>
    public interface IRemoteCommandQueue
    {
        /// <summary>
        /// Enqueues a command into the pending execution buffer.
        /// </summary>
        Task EnqueueCommandAsync(RemoteCommand command, CancellationToken ct = default);

        /// <summary>
        /// Dequeues the next highest priority pending command.
        /// </summary>
        Task<RemoteCommand?> DequeueCommandAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets the count of pending commands currently in queue.
        /// </summary>
        Task<int> GetQueueSizeAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Security service to structurally and syntactically validate a remote command.
    /// </summary>
    public interface IRemoteCommandValidator
    {
        /// <summary>
        /// Performs structural, timestamp, and syntactic validation.
        /// </summary>
        Task<bool> ValidateCommandAsync(RemoteCommand command, CancellationToken ct = default);
    }

    /// <summary>
    /// Security service verifying the cryptographic authenticity and authority of a remote command.
    /// </summary>
    public interface IRemoteCommandAuthorizationService
    {
        /// <summary>
        /// Verifies command signatures, permissions, and session tokens.
        /// </summary>
        Task<bool> AuthorizeCommandAsync(RemoteCommand command, CancellationToken ct = default);
    }

    /// <summary>
    /// Service coordinating real-time low-latency performance and session streaming telemetry.
    /// </summary>
    public interface ILiveMonitoringService
    {
        /// <summary>
        /// Connects or subscribes to live metric and state streams of a workstation.
        /// </summary>
        Task SubscribeLiveTelemetryAsync(string machineId, Func<HealthSnapshot, Task> onTelemetryReceived, CancellationToken ct = default);

        /// <summary>
        /// Disconnects from the workstation's live stream.
        /// </summary>
        Task UnsubscribeLiveTelemetryAsync(string machineId, CancellationToken ct = default);
    }

    /// <summary>
    /// Telemetry aggregator that compiles granular metric streams into high-level reports.
    /// </summary>
    public interface ITelemetryAggregator
    {
        /// <summary>
        /// Compiles and pushes workstation telemetry into long-term history and analytics.
        /// </summary>
        Task<MachineHealth> ProcessMetricsAsync(string machineId, IEnumerable<HealthSnapshot> snapshots, CancellationToken ct = default);
    }

    /// <summary>
    /// Service running comprehensive system, database, and process diagnostic investigations.
    /// </summary>
    public interface IRemoteDiagnosticsService
    {
        /// <summary>
        /// Commands a workstation to generate a specialized diagnostic report.
        /// </summary>
        Task<DiagnosticReport> GenerateReportAsync(string machineId, DiagnosticReportType reportType, CancellationToken ct = default);
    }

    /// <summary>
    /// Compression utility to compile multiple diagnostics files into a compressed payload package.
    /// </summary>
    public interface IDiagnosticPackageBuilder
    {
        /// <summary>
        /// Compiles and zips multiple diagnostics reports.
        /// </summary>
        Task<DiagnosticPackage> BuildPackageAsync(string machineId, IEnumerable<string> reportIds, CancellationToken ct = default);
    }

    /// <summary>
    /// Service for remote folder indexing, script execution, and file management.
    /// </summary>
    public interface IRemoteFileService
    {
        /// <summary>
        /// Retrieves directory listings from a remote workstation.
        /// </summary>
        Task<IReadOnlyList<string>> ListFilesAsync(string machineId, string path, CancellationToken ct = default);

        /// <summary>
        /// Deletes a file on a target remote workstation.
        /// </summary>
        Task<bool> DeleteFileAsync(string machineId, string filePath, CancellationToken ct = default);
    }

    /// <summary>
    /// Transaction coordinator for reliable parallel chunked file transfers.
    /// </summary>
    public interface ITransferManager
    {
        /// <summary>
        /// Starts a chunked file transfer job.
        /// </summary>
        Task<TransferJob> StartTransferAsync(TransferJob job, CancellationToken ct = default);

        /// <summary>
        /// Pauses or suspends an active transfer job.
        /// </summary>
        Task<bool> PauseTransferAsync(string jobId, CancellationToken ct = default);

        /// <summary>
        /// Resumes a suspended transfer job cleanly using byte range resumes.
        /// </summary>
        Task<bool> ResumeTransferAsync(string jobId, CancellationToken ct = default);

        /// <summary>
        /// Cancels and rolls back an active transfer job.
        /// </summary>
        Task<bool> CancelTransferAsync(string jobId, CancellationToken ct = default);

        /// <summary>
        /// Gets current progress statistics for an active job.
        /// </summary>
        Task<TransferProgress?> GetProgressAsync(string jobId, CancellationToken ct = default);
    }

    /// <summary>
    /// Service for creating, editing, and managing central security policy templates.
    /// </summary>
    public interface IPolicyAdministrationService
    {
        /// <summary>
        /// Saves or updates a security policy definition.
        /// </summary>
        Task<bool> SavePolicyAsync(PolicyReference policy, string contentJson, CancellationToken ct = default);

        /// <summary>
        /// Retrieves policy template contents.
        /// </summary>
        Task<string?> GetPolicyContentAsync(string policyId, string versionTag, CancellationToken ct = default);
    }

    /// <summary>
    /// Service responsible for managing workstation policy assignments.
    /// </summary>
    public interface IPolicyAssignmentService
    {
        /// <summary>
        /// Assigns a policy to a workstation or group scope.
        /// </summary>
        Task<bool> AssignPolicyAsync(string policyId, string versionTag, string targetId, CancellationToken ct = default);

        /// <summary>
        /// Removes a policy assignment from a workstation or group scope.
        /// </summary>
        Task<bool> RemovePolicyAssignmentAsync(string policyId, string targetId, CancellationToken ct = default);

        /// <summary>
        /// Gets policy assignments for a target.
        /// </summary>
        Task<IReadOnlyList<PolicyAssignment>> GetAssignmentsAsync(string targetId, CancellationToken ct = default);
    }

    /// <summary>
    /// Service evaluating overall fleet compliance with active policies.
    /// </summary>
    public interface IPolicyComplianceService
    {
        /// <summary>
        /// Performs deep compliance audits on a workstation against all assigned policy profiles.
        /// </summary>
        Task<ComplianceStatus> AuditComplianceAsync(string machineId, CancellationToken ct = default);
    }

    /// <summary>
    /// Service managing software licenses, hardware details, and inventory assets.
    /// </summary>
    public interface IAssetManagementService
    {
        /// <summary>
        /// Registers or updates an asset in the central inventory database.
        /// </summary>
        Task<bool> TrackAssetAsync(AssetRecord asset, CancellationToken ct = default);

        /// <summary>
        /// Allocates a floating software license seat.
        /// </summary>
        Task<bool> CheckoutLicenseSeatAsync(string licenseId, string machineId, CancellationToken ct = default);

        /// <summary>
        /// Returns a floating software license seat back to the pool.
        /// </summary>
        Task<bool> ReleaseLicenseSeatAsync(string licenseId, string machineId, CancellationToken ct = default);
    }

    /// <summary>
    /// Workstation sensor collector that scans and gathers hardware and software specifications.
    /// </summary>
    public interface IInventoryCollector
    {
        /// <summary>
        /// Scans and returns local hardware/software inventory details.
        /// </summary>
        Task<MachineInventory> CollectInventoryAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Service managing active execution states of scheduled workstation maintenance.
    /// </summary>
    public interface IMaintenanceService
    {
        /// <summary>
        /// Triggers maintenance execution on a workstation.
        /// </summary>
        Task<bool> ExecuteMaintenanceAsync(string machineId, string scheduleId, CancellationToken ct = default);
    }

    /// <summary>
    /// Planner service managing automatic scheduled maintenance calendars.
    /// </summary>
    public interface IMaintenanceScheduler
    {
        /// <summary>
        /// Schedules a future maintenance execution window.
        /// </summary>
        Task<bool> ScheduleMaintenanceAsync(MaintenanceSchedule schedule, CancellationToken ct = default);

        /// <summary>
        /// Cancels a pending scheduled maintenance execution window.
        /// </summary>
        Task<bool> CancelScheduledMaintenanceAsync(string scheduleId, CancellationToken ct = default);
    }

    /// <summary>
    /// Service for querying and managing administrative audit trial history.
    /// </summary>
    public interface IAuditAdministrationService
    {
        /// <summary>
        /// Queries the audit log database.
        /// </summary>
        Task<IReadOnlyList<AuditEntry>> QueryLogsAsync(string operatorId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);
    }

    /// <summary>
    /// Storage repository for transactional audit records.
    /// </summary>
    public interface IAuditStorage
    {
        /// <summary>
        /// Persists a sealed audit block record.
        /// </summary>
        Task<bool> WriteAuditRecordAsync(AuditRecord record, CancellationToken ct = default);

        /// <summary>
        /// Reads chronological audit blocks history.
        /// </summary>
        Task<IReadOnlyList<AuditRecord>> ReadAuditBlocksAsync(int pageIndex, int pageSize, CancellationToken ct = default);
    }

    /// <summary>
    /// Service coordinating bulk parallel task routing and status updates tracking.
    /// </summary>
    public interface IBulkOperationService
    {
        /// <summary>
        /// Initiates a multi-machine bulk execution operation.
        /// </summary>
        Task<string> StartBulkOperationAsync(BulkOperation operation, CancellationToken ct = default);

        /// <summary>
        /// Cancels a running bulk execution operation.
        /// </summary>
        Task<bool> CancelBulkOperationAsync(string bulkOperationId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the current progress tracking metrics.
        /// </summary>
        Task<BulkOperationProgress?> GetBulkOperationProgressAsync(string bulkOperationId, CancellationToken ct = default);
    }

    /// <summary>
    /// Thread coordinator executing multi-machine tasks with concurrency limits.
    /// </summary>
    public interface IBulkOperationCoordinator
    {
        /// <summary>
        /// Executes a single bulk command task sequence with rate-limits and retry handling.
        /// </summary>
        Task<BulkOperationResult> RunBulkOperationAsync(BulkOperation operation, int maxConcurrency, CancellationToken ct = default);
    }

    /// <summary>
    /// Service for initiating desktop assistance streaming connections.
    /// </summary>
    public interface IRemoteSupportService
    {
        /// <summary>
        /// Requests a remote support desktop streaming session.
        /// </summary>
        Task<RemoteSession> RequestSupportSessionAsync(string machineId, SupportSessionType sessionType, CancellationToken ct = default);
    }

    /// <summary>
    /// Coordinator managing streaming handshakes, participants list, and encryption keys.
    /// </summary>
    public interface IRemoteSessionManager
    {
        /// <summary>
        /// Approves or initiates a support session connection.
        /// </summary>
        Task<bool> OpenSessionAsync(string sessionId, CancellationToken ct = default);

        /// <summary>
        /// Closes and cleans up streaming session handles.
        /// </summary>
        Task<bool> CloseSessionAsync(string sessionId, CancellationToken ct = default);

        /// <summary>
        /// Registers a participant to a support session.
        /// </summary>
        Task<bool> AddParticipantAsync(string sessionId, RemoteSessionParticipant participant, CancellationToken ct = default);
    }

    /// <summary>
    /// Gateway dispatcher coordinating incoming administrative API REST routing.
    /// </summary>
    public interface IAdministrationApiService
    {
        /// <summary>
        /// Authenticates and routes an incoming remote admin HTTP request context.
        /// </summary>
        Task<string> HandleApiRequestAsync(string apiPath, string requestPayloadJson, CancellationToken ct = default);
    }

    /// <summary>
    /// Service executing complex data query aggregates to build consolidated PDF/Excel reports.
    /// </summary>
    public interface IAdministrationReportService
    {
        /// <summary>
        /// Builds an administrative analytics or fleet activity report.
        /// </summary>
        Task<AdministrationReport> GenerateAdministrationReportAsync(string reportTitle, string scopeJson, CancellationToken ct = default);
    }
}
