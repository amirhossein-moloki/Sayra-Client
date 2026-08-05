using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Sayra.Client.Shared.Fleet.Administration.Security;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Dtos;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Administration.Orchestration
{
    public interface IEnterpriseManagementCoordinator
    {
        Task<RemoteCommandResponse> ExecuteCommandWorkflowAsync(
            RemoteCommandRequest request,
            AdminUser user,
            string traceId,
            string correlationId,
            string ipAddress,
            CancellationToken ct = default);

        Task<BulkOperationResponse> StartBulkOperationWorkflowAsync(
            BulkOperationRequest request,
            AdminUser user,
            string traceId,
            string correlationId,
            string ipAddress,
            CancellationToken ct = default);

        Task<DiagnosticReport> StartDiagnosticsWorkflowAsync(
            DiagnosticRequest request,
            AdminUser user,
            string traceId,
            string correlationId,
            string ipAddress,
            CancellationToken ct = default);

        Task<bool> AssignPolicyWorkflowAsync(
            PolicyAssignmentRequest request,
            AdminUser user,
            string traceId,
            string correlationId,
            string ipAddress,
            CancellationToken ct = default);

        Task<RemoteSession> CreateSupportSessionWorkflowAsync(
            RemoteSupportRequest request,
            AdminUser user,
            string traceId,
            string correlationId,
            string ipAddress,
            CancellationToken ct = default);
    }

    public class EnterpriseManagementCoordinator : IEnterpriseManagementCoordinator
    {
        private readonly IFleetManager _fleetManager;
        private readonly IRemoteCommandService _commandService;
        private readonly IRemoteDiagnosticsService _diagnosticsService;
        private readonly IPolicyAssignmentService _policyAssignmentService;
        private readonly IBulkOperationService _bulkOperationService;
        private readonly IRemoteSupportService _remoteSupportService;
        private readonly IAuditIntegrationService _auditService;
        private readonly IAdministrationNotificationService _notificationService;
        private readonly IValidator<RemoteCommandRequest> _commandValidator;
        private readonly IValidator<BulkOperationRequest> _bulkValidator;
        private readonly IValidator<DiagnosticRequest> _diagnosticValidator;
        private readonly IValidator<PolicyAssignmentRequest> _policyValidator;
        private readonly IValidator<RemoteSupportRequest> _supportValidator;

        public EnterpriseManagementCoordinator(
            IFleetManager fleetManager,
            IRemoteCommandService commandService,
            IRemoteDiagnosticsService diagnosticsService,
            IPolicyAssignmentService policyAssignmentService,
            IBulkOperationService bulkOperationService,
            IRemoteSupportService remoteSupportService,
            IAuditIntegrationService auditService,
            IAdministrationNotificationService notificationService,
            IValidator<RemoteCommandRequest> commandValidator,
            IValidator<BulkOperationRequest> bulkValidator,
            IValidator<DiagnosticRequest> diagnosticValidator,
            IValidator<PolicyAssignmentRequest> policyValidator,
            IValidator<RemoteSupportRequest> supportValidator)
        {
            _fleetManager = fleetManager;
            _commandService = commandService;
            _diagnosticsService = diagnosticsService;
            _policyAssignmentService = policyAssignmentService;
            _bulkOperationService = bulkOperationService;
            _remoteSupportService = remoteSupportService;
            _auditService = auditService;
            _notificationService = notificationService;
            _commandValidator = commandValidator;
            _bulkValidator = bulkValidator;
            _diagnosticValidator = diagnosticValidator;
            _policyValidator = policyValidator;
            _supportValidator = supportValidator;
        }

        public async Task<RemoteCommandResponse> ExecuteCommandWorkflowAsync(
            RemoteCommandRequest request,
            AdminUser user,
            string traceId,
            string correlationId,
            string ipAddress,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            // 1. Validation
            var valResult = await _commandValidator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                var errors = string.Join("; ", valResult.Errors.Select(e => e.ErrorMessage));
                await _auditService.LogActionAsync(user.AdministratorId, request.MachineId, "EXECUTE_COMMAND", $"Action: {request.Action}", sw.ElapsedMilliseconds, "ValidationError", errors, ipAddress, correlationId);
                throw new ValidationException(valResult.Errors);
            }

            try
            {
                // 2. Map & Execute
                var cmd = new RemoteCommand
                {
                    CommandId = Guid.NewGuid().ToString("N"),
                    Action = request.Action,
                    TargetMachineId = request.MachineId,
                    Priority = Enum.TryParse<CommandPriority>(request.Priority, true, out var p) ? p : CommandPriority.Normal,
                    Parameters = request.Parameters.Select(kv => new CommandParameter { Name = kv.Key, Value = kv.Value, IsSecure = kv.Key.Contains("password", StringComparison.OrdinalIgnoreCase) || kv.Key.Contains("token", StringComparison.OrdinalIgnoreCase) }).ToList(),
                    Signature = request.Signature,
                    CreatorOperatorId = user.AdministratorId,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
                };

                var result = await _commandService.ExecuteCommandAsync(cmd, ct);

                sw.Stop();

                // 3. Log Audit
                await _auditService.LogActionAsync(
                    user.AdministratorId,
                    request.MachineId,
                    "EXECUTE_COMMAND",
                    $"Action: {request.Action}",
                    sw.ElapsedMilliseconds,
                    result.Outcome.ToString(),
                    result.OutputMessage,
                    ipAddress,
                    correlationId);

                // 4. Publish Notification
                if (result.Outcome == OperationResult.Success)
                {
                    await _notificationService.PublishNotificationAsync("Operation Completed", $"Command {request.Action} executed successfully on {request.MachineId}");
                }
                else
                {
                    await _notificationService.PublishNotificationAsync("Operation Failed", $"Command {request.Action} failed on {request.MachineId}: {result.OutputMessage}");
                }

                return new RemoteCommandResponse
                {
                    CommandId = cmd.CommandId,
                    Status = result.Status.ToString(),
                    Outcome = result.Outcome.ToString(),
                    OutputMessage = result.OutputMessage
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                await _auditService.LogActionAsync(user.AdministratorId, request.MachineId, "EXECUTE_COMMAND", $"Action: {request.Action}", sw.ElapsedMilliseconds, "Failure", ex.Message, ipAddress, correlationId);
                await _notificationService.PublishNotificationAsync("Operation Failed", $"Command {request.Action} failed on {request.MachineId}: {ex.Message}");
                throw;
            }
        }

        public async Task<BulkOperationResponse> StartBulkOperationWorkflowAsync(
            BulkOperationRequest request,
            AdminUser user,
            string traceId,
            string correlationId,
            string ipAddress,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            var valResult = await _bulkValidator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                var errors = string.Join("; ", valResult.Errors.Select(e => e.ErrorMessage));
                await _auditService.LogActionAsync(user.AdministratorId, null, "START_BULK_OPERATION", $"Action: {request.Action}", sw.ElapsedMilliseconds, "ValidationError", errors, ipAddress, correlationId);
                throw new ValidationException(valResult.Errors);
            }

            try
            {
                // Resolve all targets
                var targetMachineIds = new HashSet<string>(request.MachineIds);
                foreach (var groupId in request.GroupIds)
                {
                    var members = await _fleetManager.GetGroupMembersAsync(groupId, ct);
                    foreach (var m in members)
                    {
                        targetMachineIds.Add(m.MachineId);
                    }
                }

                var bulkOp = new BulkOperation
                {
                    BulkOperationId = Guid.NewGuid().ToString("N"),
                    Action = request.Action,
                    TargetMachineIds = targetMachineIds.ToList(),
                    Status = OperationStatus.Pending,
                    OperatorId = user.AdministratorId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var bulkId = await _bulkOperationService.StartBulkOperationAsync(bulkOp, ct);

                sw.Stop();

                await _auditService.LogActionAsync(
                    user.AdministratorId,
                    null,
                    "START_BULK_OPERATION",
                    $"Action: {request.Action}, ID: {bulkId}, Count: {targetMachineIds.Count}",
                    sw.ElapsedMilliseconds,
                    "Success",
                    null,
                    ipAddress,
                    correlationId);

                await _notificationService.PublishNotificationAsync("Operation Completed", $"Bulk operation {request.Action} started with ID {bulkId} targeting {targetMachineIds.Count} machines.");

                return new BulkOperationResponse
                {
                    BulkOperationId = bulkId,
                    Status = "Running",
                    TotalTargets = targetMachineIds.Count,
                    SucceededCount = 0,
                    FailedCount = 0
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                await _auditService.LogActionAsync(user.AdministratorId, null, "START_BULK_OPERATION", $"Action: {request.Action}", sw.ElapsedMilliseconds, "Failure", ex.Message, ipAddress, correlationId);
                await _notificationService.PublishNotificationAsync("Operation Failed", $"Bulk operation {request.Action} failed to start: {ex.Message}");
                throw;
            }
        }

        public async Task<DiagnosticReport> StartDiagnosticsWorkflowAsync(
            DiagnosticRequest request,
            AdminUser user,
            string traceId,
            string correlationId,
            string ipAddress,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            var valResult = await _diagnosticValidator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                var errors = string.Join("; ", valResult.Errors.Select(e => e.ErrorMessage));
                await _auditService.LogActionAsync(user.AdministratorId, request.MachineId, "START_DIAGNOSTICS", $"Type: {request.ReportType}", sw.ElapsedMilliseconds, "ValidationError", errors, ipAddress, correlationId);
                throw new ValidationException(valResult.Errors);
            }

            try
            {
                var reportType = Enum.TryParse<DiagnosticReportType>(request.ReportType, true, out var t) ? t : DiagnosticReportType.GeneralHealth;
                var report = await _diagnosticsService.GenerateReportAsync(request.MachineId, reportType, ct);

                sw.Stop();

                await _auditService.LogActionAsync(
                    user.AdministratorId,
                    request.MachineId,
                    "START_DIAGNOSTICS",
                    $"Type: {request.ReportType}, ReportID: {report.ReportId}",
                    sw.ElapsedMilliseconds,
                    "Success",
                    null,
                    ipAddress,
                    correlationId);

                await _notificationService.PublishNotificationAsync("Operation Completed", $"Diagnostics report of type {request.ReportType} completed successfully for {request.MachineId}");

                return report;
            }
            catch (Exception ex)
            {
                sw.Stop();
                await _auditService.LogActionAsync(user.AdministratorId, request.MachineId, "START_DIAGNOSTICS", $"Type: {request.ReportType}", sw.ElapsedMilliseconds, "Failure", ex.Message, ipAddress, correlationId);
                await _notificationService.PublishNotificationAsync("Operation Failed", $"Diagnostics on {request.MachineId} failed: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> AssignPolicyWorkflowAsync(
            PolicyAssignmentRequest request,
            AdminUser user,
            string traceId,
            string correlationId,
            string ipAddress,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            var valResult = await _policyValidator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                var errors = string.Join("; ", valResult.Errors.Select(e => e.ErrorMessage));
                await _auditService.LogActionAsync(user.AdministratorId, null, "ASSIGN_POLICY", $"Policy: {request.PolicyId}, Target: {request.TargetId}", sw.ElapsedMilliseconds, "ValidationError", errors, ipAddress, correlationId);
                throw new ValidationException(valResult.Errors);
            }

            try
            {
                var result = await _policyAssignmentService.AssignPolicyAsync(request.PolicyId, request.VersionTag, request.TargetId, ct);

                sw.Stop();

                await _auditService.LogActionAsync(
                    user.AdministratorId,
                    null,
                    "ASSIGN_POLICY",
                    $"Policy: {request.PolicyId}, Version: {request.VersionTag}, Target: {request.TargetId}",
                    sw.ElapsedMilliseconds,
                    result ? "Success" : "Failure",
                    result ? null : "Assignment Service rejected policy assignment",
                    ipAddress,
                    correlationId);

                await _notificationService.PublishNotificationAsync("Policy Violation", $"New policy assignment applied: {request.PolicyId} (v{request.VersionTag}) to target {request.TargetId}");

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                await _auditService.LogActionAsync(user.AdministratorId, null, "ASSIGN_POLICY", $"Policy: {request.PolicyId}, Target: {request.TargetId}", sw.ElapsedMilliseconds, "Failure", ex.Message, ipAddress, correlationId);
                throw;
            }
        }

        public async Task<RemoteSession> CreateSupportSessionWorkflowAsync(
            RemoteSupportRequest request,
            AdminUser user,
            string traceId,
            string correlationId,
            string ipAddress,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            var valResult = await _supportValidator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                var errors = string.Join("; ", valResult.Errors.Select(e => e.ErrorMessage));
                await _auditService.LogActionAsync(user.AdministratorId, request.MachineId, "CREATE_SUPPORT_SESSION", $"Type: {request.SessionType}", sw.ElapsedMilliseconds, "ValidationError", errors, ipAddress, correlationId);
                throw new ValidationException(valResult.Errors);
            }

            try
            {
                var sessionType = Enum.TryParse<SupportSessionType>(request.SessionType, true, out var t) ? t : SupportSessionType.TerminalOnly;
                var session = await _remoteSupportService.RequestSupportSessionAsync(request.MachineId, sessionType, ct);

                sw.Stop();

                await _auditService.LogActionAsync(
                    user.AdministratorId,
                    request.MachineId,
                    "CREATE_SUPPORT_SESSION",
                    $"SessionID: {session.SessionId}, Type: {request.SessionType}, Permission: {request.RequestedPermission}",
                    sw.ElapsedMilliseconds,
                    "Success",
                    null,
                    ipAddress,
                    correlationId);

                await _notificationService.PublishNotificationAsync("Security Alert", $"Remote support session {session.SessionId} was initiated on {request.MachineId} by {user.Username}");

                return session;
            }
            catch (Exception ex)
            {
                sw.Stop();
                await _auditService.LogActionAsync(user.AdministratorId, request.MachineId, "CREATE_SUPPORT_SESSION", $"Type: {request.SessionType}", sw.ElapsedMilliseconds, "Failure", ex.Message, ipAddress, correlationId);
                throw;
            }
        }
    }
}
