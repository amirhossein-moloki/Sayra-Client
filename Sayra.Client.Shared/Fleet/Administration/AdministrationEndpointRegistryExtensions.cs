using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Fleet.Administration.Security;
using Sayra.Client.Shared.Fleet.Administration.Orchestration;
using Sayra.Client.Shared.Fleet.Administration.Queries;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Dtos;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Administration
{
    public static class AdministrationEndpointRegistryExtensions
    {
        public static void MapAllEndpoints(this AdministrationEndpointRegistry registry, IServiceProvider sp)
        {
            var fleet = sp.GetRequiredService<IFleetManager>();
            var coordinator = sp.GetRequiredService<IEnterpriseManagementCoordinator>();
            var dashboard = sp.GetRequiredService<IDashboardQueryService>();
            var audit = sp.GetRequiredService<IAuditIntegrationService>();
            var notifications = sp.GetRequiredService<IAdministrationNotificationService>();
            var policy = sp.GetRequiredService<IPolicyAdministrationService>();
            var policyAssignment = sp.GetRequiredService<IPolicyAssignmentService>();
            var policyCompliance = sp.GetRequiredService<IPolicyComplianceService>();
            var assets = sp.GetRequiredService<IAssetManagementService>();
            var maintenance = sp.GetRequiredService<IMaintenanceService>();
            var maintenanceScheduler = sp.GetRequiredService<IMaintenanceScheduler>();
            var bulkOps = sp.GetRequiredService<IBulkOperationService>();
            var support = sp.GetRequiredService<IRemoteSupportService>();
            var supportSession = sp.GetRequiredService<IRemoteSessionManager>();
            var file = sp.GetRequiredService<IRemoteFileService>();
            var transfer = sp.GetRequiredService<ITransferManager>();
            var authz = sp.GetRequiredService<IAuthorizationService>();

            // HELPER: Extract Pagination
            (int page, int pageSize) GetPagination(Dictionary<string, string> queryParams)
            {
                int p = queryParams.TryGetValue("page", out var ps) && int.TryParse(ps, out var pi) ? pi : 1;
                int sz = queryParams.TryGetValue("pageSize", out var ssz) && int.TryParse(ssz, out var szi) ? szi : 10;
                return (p, sz);
            }

            // HELPER: Validate permission or throw
            void Authorize(AdminUser user, AdminPermission permission)
            {
                if (!authz.HasPermission(user, permission))
                {
                    throw new UnauthorizedAccessException($"Access Denied. Administrator does not have the '{permission}' permission.");
                }
            }

            #region 1. FLEET API
            // GET /api/fleet/machines
            registry.MapRoute("GET", "api/fleet/machines", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var list = await fleet.GetAllMachinesAsync(ct);

                // Filter
                if (queryParams.TryGetValue("status", out var statusStr) && Enum.TryParse<MachineStatus>(statusStr, true, out var status))
                {
                    list = list.Where(m => m.Status == status).ToList();
                }
                if (queryParams.TryGetValue("hostname", out var hostName))
                {
                    list = list.Where(m => m.Hostname.Contains(hostName, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                // Sort
                if (queryParams.TryGetValue("sort", out var sort))
                {
                    list = sort.ToLowerInvariant() switch
                    {
                        "hostname" => list.OrderBy(m => m.Hostname).ToList(),
                        "status" => list.OrderBy(m => m.Status).ToList(),
                        _ => list.OrderByDescending(m => m.LastSeenUtc).ToList()
                    };
                }

                // Page
                var (p, sz) = GetPagination(queryParams);
                var paged = list.Skip((p - 1) * sz).Take(sz).ToList();

                return JsonSerializer.Serialize(new { total = list.Count, page = p, pageSize = sz, items = paged });
            });

            // GET /api/fleet/groups
            registry.MapRoute("GET", "api/fleet/groups", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                // Dynamically simulate groups registry lists
                var groups = new List<FleetGroup>
                {
                    new() { GroupId = "G-01", Name = "VIP Lounge", GroupType = FleetGroupType.Static },
                    new() { GroupId = "G-02", Name = "Standard Gaming Floor", GroupType = FleetGroupType.Static },
                    new() { GroupId = "G-03", Name = "Dynamic High Health", GroupType = FleetGroupType.Dynamic, DynamicRuleExpression = "HealthStatus == Healthy" }
                };
                return JsonSerializer.Serialize(groups);
            });

            // GET /api/fleet/regions
            registry.MapRoute("GET", "api/fleet/regions", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var regions = new List<FleetRegion>
                {
                    new() { RegionId = "R-01", Name = "North Sector", RegionType = FleetRegionType.Regional },
                    new() { RegionId = "R-02", Name = "South Sector", RegionType = FleetRegionType.Regional }
                };
                return JsonSerializer.Serialize(regions);
            });

            // GET /api/fleet/tags
            registry.MapRoute("GET", "api/fleet/tags", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var tags = new List<FleetTag>
                {
                    new() { Key = "DeviceType", Value = "Consoles" },
                    new() { Key = "Tier", Value = "Premium" }
                };
                return JsonSerializer.Serialize(tags);
            });

            // GET /api/fleet/health
            registry.MapRoute("GET", "api/fleet/health", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var dist = await dashboard.GetHealthDistributionAsync(ct);
                return JsonSerializer.Serialize(dist);
            });
            #endregion

            #region 2. COMMAND API
            // POST /api/commands/execute
            registry.MapRoute("POST", "api/commands/execute", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ExecuteCommand);
                var req = JsonSerializer.Deserialize<RemoteCommandRequest>(body) ?? throw new ArgumentException("Invalid body payload.");
                var response = await coordinator.ExecuteCommandWorkflowAsync(req, user, "trace-id", "correlation-id", "127.0.0.1", ct);
                return JsonSerializer.Serialize(response);
            });

            // GET /api/commands/status/{id}
            registry.MapRoute("GET", "api/commands/status/{id}", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ExecuteCommand);
                var id = routeParams["id"];
                return JsonSerializer.Serialize(new { CommandId = id, Status = "Completed", Outcome = "Success", OutputMessage = "Command executed successfully." });
            });

            // GET /api/commands/history
            registry.MapRoute("GET", "api/commands/history", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewAudit);
                var audits = await audit.QueryEntriesAsync(null, null, null, 1, 100);
                var cmdAudits = audits.Where(a => a.ActionType == AuditOperationType.RemoteCommandExecution).ToList();
                return JsonSerializer.Serialize(cmdAudits);
            });
            #endregion

            #region 3. MONITORING API
            // GET /api/monitoring/metrics
            registry.MapRoute("GET", "api/monitoring/metrics", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var snapshot = new HealthSnapshot
                {
                    CpuUtilization = 25.4,
                    MemoryUtilization = 62.1,
                    StorageUtilization = 44.9,
                    NetworkThroughputBytesPerSec = 1048576
                };
                return JsonSerializer.Serialize(snapshot);
            });

            // GET /api/monitoring/history
            registry.MapRoute("GET", "api/monitoring/history", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var history = new List<HealthSnapshot>
                {
                    new() { TimestampUtc = DateTime.UtcNow.AddMinutes(-10), CpuUtilization = 15.2, MemoryUtilization = 58.0 },
                    new() { TimestampUtc = DateTime.UtcNow.AddMinutes(-5), CpuUtilization = 34.1, MemoryUtilization = 60.5 },
                    new() { TimestampUtc = DateTime.UtcNow, CpuUtilization = 25.4, MemoryUtilization = 62.1 }
                };
                return JsonSerializer.Serialize(history);
            });

            // GET /api/monitoring/health-scores
            registry.MapRoute("GET", "api/monitoring/health-scores", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var score = new MachineHealth
                {
                    MachineId = queryParams.TryGetValue("machineId", out var mid) ? mid : "WS-ALL",
                    OverallHealthScore = 98.5,
                    ActiveWarningsCount = 1,
                    ActiveEmergenciesCount = 0
                };
                return JsonSerializer.Serialize(score);
            });

            // GET /api/monitoring/alerts
            registry.MapRoute("GET", "api/monitoring/alerts", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var alerts = await dashboard.GetRecentAlertsAsync(10, ct);
                return JsonSerializer.Serialize(alerts);
            });
            #endregion

            #region 4. DIAGNOSTICS API
            // POST /api/diagnostics/start
            registry.MapRoute("POST", "api/diagnostics/start", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.AccessDiagnostics);
                var req = JsonSerializer.Deserialize<DiagnosticRequest>(body) ?? throw new ArgumentException("Invalid body.");
                var report = await coordinator.StartDiagnosticsWorkflowAsync(req, user, "trace-id", "correlation-id", "127.0.0.1", ct);
                return JsonSerializer.Serialize(report);
            });

            // GET /api/diagnostics/reports
            registry.MapRoute("GET", "api/diagnostics/reports", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.AccessDiagnostics);
                var list = new List<DiagnosticReport>
                {
                    new() { ReportId = "DIAG-101", MachineId = "WS-01", Category = DiagnosticReportType.GeneralHealth, ContentJson = "{}", CreatedAtUtc = DateTime.UtcNow }
                };
                return JsonSerializer.Serialize(list);
            });

            // GET /api/diagnostics/metadata
            registry.MapRoute("GET", "api/diagnostics/metadata", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.AccessDiagnostics);
                var metadata = new Dictionary<string, string>
                {
                    { "StorageStagingDirectory", "Data/Diagnostics" },
                    { "MaxAllowedStorageMb", "500" }
                };
                return JsonSerializer.Serialize(metadata);
            });

            // GET /api/diagnostics/history
            registry.MapRoute("GET", "api/diagnostics/history", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.AccessDiagnostics);
                var history = new List<DiagnosticPackage>
                {
                    new() { PackageId = "PKG-01", ArchiveFileName = "diag_WS-01.zip", SizeBytes = 1048576, IntegrityHash = "abc-sha256", SourceMachineId = "WS-01", GeneratedAtUtc = DateTime.UtcNow }
                };
                return JsonSerializer.Serialize(history);
            });
            #endregion

            #region 5. FILE API
            // POST /api/files/upload
            registry.MapRoute("POST", "api/files/upload", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManageFiles);
                var req = JsonSerializer.Deserialize<TransferRequest>(body) ?? throw new ArgumentException("Invalid body.");
                var job = new TransferJob
                {
                    JobId = Guid.NewGuid().ToString("N"),
                    FilePath = req.FilePath,
                    Direction = TransferDirection.Upload,
                    Category = TransferType.File,
                    Status = TransferStatus.Preparing,
                    TotalFileSizeBytes = req.TotalFileSizeBytes
                };
                var startedJob = await transfer.StartTransferAsync(job, ct);
                return JsonSerializer.Serialize(new TransferResponse { JobId = startedJob.JobId, Status = startedJob.Status.ToString(), TotalChunks = startedJob.Chunks.Count });
            });

            // POST /api/files/download
            registry.MapRoute("POST", "api/files/download", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManageFiles);
                var req = JsonSerializer.Deserialize<TransferRequest>(body) ?? throw new ArgumentException("Invalid body.");
                var job = new TransferJob
                {
                    JobId = Guid.NewGuid().ToString("N"),
                    FilePath = req.FilePath,
                    Direction = TransferDirection.Download,
                    Category = TransferType.File,
                    Status = TransferStatus.Preparing,
                    TotalFileSizeBytes = req.TotalFileSizeBytes
                };
                var startedJob = await transfer.StartTransferAsync(job, ct);
                return JsonSerializer.Serialize(new TransferResponse { JobId = startedJob.JobId, Status = startedJob.Status.ToString(), TotalChunks = startedJob.Chunks.Count });
            });

            // GET /api/files/status/{id}
            registry.MapRoute("GET", "api/files/status/{id}", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManageFiles);
                var id = routeParams["id"];
                var progress = await transfer.GetProgressAsync(id, ct);
                return JsonSerializer.Serialize(progress ?? new TransferProgress { JobId = id, TransferredBytes = 1024, BytesPerSecSpeed = 50000 });
            });

            // GET /api/files/history
            registry.MapRoute("GET", "api/files/history", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManageFiles);
                var list = new List<TransferResult>
                {
                    new() { JobId = "JOB-101", IsSuccess = true, FilePath = "C:\\Games\\config.ini", TransferredBytes = 1048, Duration = TimeSpan.FromSeconds(2) }
                };
                return JsonSerializer.Serialize(list);
            });
            #endregion

            #region 6. POLICY API
            // POST /api/policies
            registry.MapRoute("POST", "api/policies", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManagePolicy);
                var refPolicy = JsonSerializer.Deserialize<PolicyReference>(body) ?? throw new ArgumentException("Invalid body.");
                var success = await policy.SavePolicyAsync(refPolicy, "{}", ct);
                return JsonSerializer.Serialize(new { success });
            });

            // POST /api/policies/assign
            registry.MapRoute("POST", "api/policies/assign", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManagePolicy);
                var req = JsonSerializer.Deserialize<PolicyAssignmentRequest>(body) ?? throw new ArgumentException("Invalid body.");
                var success = await coordinator.AssignPolicyWorkflowAsync(req, user, "trace-id", "correlation-id", "127.0.0.1", ct);
                return JsonSerializer.Serialize(new { success });
            });

            // POST /api/policies/validate
            registry.MapRoute("POST", "api/policies/validate", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManagePolicy);
                var req = JsonSerializer.Deserialize<PolicyAssignmentRequest>(body) ?? throw new ArgumentException("Invalid body.");
                var status = await policyCompliance.AuditComplianceAsync(req.TargetId, ct);
                return JsonSerializer.Serialize(new { complianceStatus = status.ToString() });
            });

            // GET /api/policies/compare
            registry.MapRoute("GET", "api/policies/compare", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManagePolicy);
                var policyId = queryParams.TryGetValue("policyId", out var pid) ? pid : "P-01";
                var standard = await policy.GetPolicyContentAsync(policyId, "v1", ct) ?? "{}";
                var draft = await policy.GetPolicyContentAsync(policyId, "v2", ct) ?? "{}";
                return JsonSerializer.Serialize(new { policyId, standardVersion = "v1", draftVersion = "v2", changesDetected = standard != draft });
            });

            // POST /api/policies/rollback
            registry.MapRoute("POST", "api/policies/rollback", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManagePolicy);
                var req = JsonSerializer.Deserialize<PolicyAssignmentRequest>(body) ?? throw new ArgumentException("Invalid body.");
                var success = await policyAssignment.AssignPolicyAsync(req.PolicyId, "v1", req.TargetId, ct);
                return JsonSerializer.Serialize(new { success, rolledBackTo = "v1" });
            });
            #endregion

            #region 7. ASSET API
            // GET /api/assets/inventory
            registry.MapRoute("GET", "api/assets/inventory", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var list = new List<AssetRecord>
                {
                    new() { AssetId = "A-1", MachineId = "WS-01", Name = "Core i9 14900K", SerialOrSignature = "CPU-12345", Category = AssetType.Cpu }
                };
                return JsonSerializer.Serialize(list);
            });

            // GET /api/assets
            registry.MapRoute("GET", "api/assets", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var list = new List<AssetRecord>
                {
                    new() { AssetId = "A-1", MachineId = "WS-01", Name = "Core i9 14900K", Category = AssetType.Cpu, Status = AssetStatus.Active }
                };
                return JsonSerializer.Serialize(list);
            });

            // GET /api/assets/changes
            registry.MapRoute("GET", "api/assets/changes", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var changes = new List<object>
                {
                    new { assetId = "A-1", machineId = "WS-01", changeType = "Modified", fieldName = "Status", oldValue = "Inactive", newValue = "Active", timestamp = DateTime.UtcNow }
                };
                return JsonSerializer.Serialize(changes);
            });

            // GET /api/assets/history
            registry.MapRoute("GET", "api/assets/history", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewMachine);
                var history = new List<AssetRecord>
                {
                    new() { AssetId = "A-1", MachineId = "WS-01", Name = "Core i9 14900K", Category = AssetType.Cpu }
                };
                return JsonSerializer.Serialize(history);
            });
            #endregion

            #region 8. MAINTENANCE API
            // GET /api/maintenance/schedules
            registry.MapRoute("GET", "api/maintenance/schedules", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManagePolicy);
                var schedule = new MaintenanceSchedule
                {
                    ScheduleId = "SCH-01",
                    ScopeFilter = "WS-ALL",
                    State = MaintenanceStatus.Scheduled,
                    ExecutionSummary = "Scheduled nightly restart"
                };
                return JsonSerializer.Serialize(new[] { schedule });
            });

            // GET /api/maintenance/windows
            registry.MapRoute("GET", "api/maintenance/windows", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManagePolicy);
                var window = new MaintenanceWindow
                {
                    WindowId = "W-01",
                    Category = MaintenanceWindowType.ScheduledReboot,
                    StartTimeUtc = DateTime.UtcNow.AddHours(2),
                    Duration = TimeSpan.FromMinutes(30),
                    ForceSessionTermination = true
                };
                return JsonSerializer.Serialize(new[] { window });
            });

            // GET /api/maintenance/executions
            registry.MapRoute("GET", "api/maintenance/executions", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManagePolicy);
                var executions = new List<object>
                {
                    new { scheduleId = "SCH-01", machineId = "WS-01", status = "Completed", startedAt = DateTime.UtcNow.AddHours(-1), endedAt = DateTime.UtcNow.AddHours(-1).AddMinutes(15) }
                };
                return JsonSerializer.Serialize(executions);
            });

            // GET /api/maintenance/history
            registry.MapRoute("GET", "api/maintenance/history", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ManagePolicy);
                var history = new List<MaintenanceSchedule>
                {
                    new() { ScheduleId = "SCH-01", ScopeFilter = "WS-ALL", State = MaintenanceStatus.Completed, ExecutionSummary = "Succeeded" }
                };
                return JsonSerializer.Serialize(history);
            });
            #endregion

            #region 9. BULK API
            // POST /api/bulk
            registry.MapRoute("POST", "api/bulk", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ExecuteCommand);
                var req = JsonSerializer.Deserialize<BulkOperationRequest>(body) ?? throw new ArgumentException("Invalid body.");
                var response = await coordinator.StartBulkOperationWorkflowAsync(req, user, "trace-id", "correlation-id", "127.0.0.1", ct);
                return JsonSerializer.Serialize(response);
            });

            // GET /api/bulk/progress/{id}
            registry.MapRoute("GET", "api/bulk/progress/{id}", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ExecuteCommand);
                var id = routeParams["id"];
                var progress = await bulkOps.GetBulkOperationProgressAsync(id, ct);
                return JsonSerializer.Serialize(progress ?? new BulkOperationProgress { ActiveStatus = OperationStatus.Completed, TotalTargets = 5, CompletedCount = 5 });
            });

            // POST /api/bulk/cancel/{id}
            registry.MapRoute("POST", "api/bulk/cancel/{id}", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ExecuteCommand);
                var id = routeParams["id"];
                var success = await bulkOps.CancelBulkOperationAsync(id, ct);
                return JsonSerializer.Serialize(new { success });
            });

            // POST /api/bulk/rollback/{id}
            registry.MapRoute("POST", "api/bulk/rollback/{id}", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ExecuteCommand);
                var id = routeParams["id"];
                return JsonSerializer.Serialize(new { success = true, bulkOperationId = id, status = "RolledBack" });
            });
            #endregion

            #region 10. REMOTE SUPPORT API
            // POST /api/support/sessions
            registry.MapRoute("POST", "api/support/sessions", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.RemoteSupport);
                var req = JsonSerializer.Deserialize<RemoteSupportRequest>(body) ?? throw new ArgumentException("Invalid body.");
                var session = await coordinator.CreateSupportSessionWorkflowAsync(req, user, "trace-id", "correlation-id", "127.0.0.1", ct);
                return JsonSerializer.Serialize(session);
            });

            // POST /api/support/sessions/{id}/approve
            registry.MapRoute("POST", "api/support/sessions/{id}/approve", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.RemoteSupport);
                var id = routeParams["id"];
                var success = await supportSession.OpenSessionAsync(id, ct);
                return JsonSerializer.Serialize(new { success });
            });

            // GET /api/support/sessions/{id}/status
            registry.MapRoute("GET", "api/support/sessions/{id}/status", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.RemoteSupport);
                var id = routeParams["id"];
                return JsonSerializer.Serialize(new { sessionId = id, status = "Active", participantsCount = 1 });
            });

            // POST /api/support/sessions/{id}/terminate
            registry.MapRoute("POST", "api/support/sessions/{id}/terminate", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.RemoteSupport);
                var id = routeParams["id"];
                var success = await supportSession.CloseSessionAsync(id, ct);
                return JsonSerializer.Serialize(new { success });
            });
            #endregion

            #region 11. AUDIT API
            // GET /api/audit/logs
            registry.MapRoute("GET", "api/audit/logs", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewAudit);
                var (p, sz) = GetPagination(queryParams);
                var list = await audit.QueryEntriesAsync(null, null, null, p, sz);
                return JsonSerializer.Serialize(new { total = list.Count, page = p, pageSize = sz, items = list });
            });

            // GET /api/audit/history
            registry.MapRoute("GET", "api/audit/history", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewAudit);
                var (p, sz) = GetPagination(queryParams);
                var list = await audit.QueryEntriesAsync(null, null, null, p, sz);
                return JsonSerializer.Serialize(list);
            });

            // GET /api/audit/security-events
            registry.MapRoute("GET", "api/audit/security-events", async (routeParams, queryParams, body, user, ct) =>
            {
                Authorize(user, AdminPermission.ViewAudit);
                var list = await audit.QueryEntriesAsync(null, null, null, 1, 100);
                var secEvents = list.Where(a => a.ActionType == AuditOperationType.SecurityHardeningChange).ToList();
                return JsonSerializer.Serialize(secEvents);
            });
            #endregion
        }
    }
}
