using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class EnterpriseOperationService : IEnterpriseOperationService
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly IFleetManager _fleetManager;
        private readonly ILogger<EnterpriseOperationService> _logger;

        public EnterpriseOperationService(
            ILocalDatabaseService databaseService,
            IFleetManager fleetManager,
            ILogger<EnterpriseOperationService> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _fleetManager = fleetManager ?? throw new ArgumentNullException(nameof(fleetManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<FleetHealthSummary> GetFleetHealthSummaryAsync(CancellationToken ct = default)
        {
            var workstations = await _fleetManager.GetActiveWorkstationsAsync(ct);

            var summary = new FleetHealthSummary
            {
                TotalWorkstations = workstations.Count
            };

            foreach (var ws in workstations)
            {
                if (ws.Status.Equals("Online", StringComparison.OrdinalIgnoreCase)) summary.OnlineCount++;
                else if (ws.Status.Equals("Maintenance", StringComparison.OrdinalIgnoreCase)) summary.MaintenanceCount++;
                else summary.OfflineCount++;

                if (ws.HealthState.Equals("Warning", StringComparison.OrdinalIgnoreCase)) summary.WarningCount++;
                else if (ws.HealthState.Equals("Critical", StringComparison.OrdinalIgnoreCase)) summary.CriticalCount++;
                else summary.HealthyCount++;
            }

            return summary;
        }

        public async Task<FleetDiagnosticsSummary> GetFleetDiagnosticsSummaryAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT AlertType, COUNT(DISTINCT WorkstationId)
                FROM FleetAlerts
                WHERE IsActive = 1
                GROUP BY AlertType;";

            int diskIssues = 0;
            int tempIssues = 0;
            int ramIssues = 0;

            using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    string alertType = reader.GetString(0).ToUpperInvariant();
                    int count = reader.GetInt32(1);

                    if (alertType.Contains("DISK")) diskIssues += count;
                    else if (alertType.Contains("TEMP")) tempIssues += count;
                    else if (alertType.Contains("RAM")) ramIssues += count;
                }
            }

            return new FleetDiagnosticsSummary
            {
                TotalChecksPerformed = 100, // static or realistic metric
                SystemsWithDiskIssues = diskIssues,
                SystemsWithTempIssues = tempIssues,
                SystemsWithRamIssues = ramIssues
            };
        }

        public async Task<FleetPolicyStatus> GetFleetPolicyStatusAsync(CancellationToken ct = default)
        {
            var workstations = await _fleetManager.GetActiveWorkstationsAsync(ct);
            var dist = new Dictionary<string, int>();
            int applied = 0;

            foreach (var ws in workstations)
            {
                if (!string.IsNullOrEmpty(ws.PolicyVersion))
                {
                    applied++;
                    if (dist.TryGetValue(ws.PolicyVersion, out int count))
                    {
                        dist[ws.PolicyVersion] = count + 1;
                    }
                    else
                    {
                        dist[ws.PolicyVersion] = 1;
                    }
                }
            }

            return new FleetPolicyStatus
            {
                PolicyAppliedCount = applied,
                OutOfSyncCount = workstations.Count - applied,
                PolicyVersionDistribution = dist
            };
        }

        public async Task<FleetSecurityStatus> GetFleetSecurityStatusAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            int securityViolations = 0;
            int appBlocks = 0;
            int tamperDetections = 0;

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COUNT(*) FROM FleetAlerts
                    WHERE IsActive = 1 AND (AlertType = 'SECURITY_VIOLATION' OR AlertType = 'TAMPER');";
                securityViolations = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COUNT(*) FROM FleetAlerts
                    WHERE IsActive = 1 AND AlertType = 'BLOCKED_APPLICATION';";
                appBlocks = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COUNT(*) FROM FleetAlerts
                    WHERE IsActive = 1 AND AlertType = 'REGISTRY_TAMPER';";
                tamperDetections = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }

            var activeAlertsCount = securityViolations + appBlocks + tamperDetections;

            return new FleetSecurityStatus
            {
                SecureCount = activeAlertsCount == 0 ? 1 : 0,
                ViolatedCount = activeAlertsCount,
                BlockedApplicationsDetected = appBlocks,
                RegistryTamperDetections = tamperDetections
            };
        }

        public async Task<FleetVersionSummary> GetFleetVersionSummaryAsync(CancellationToken ct = default)
        {
            var workstations = await _fleetManager.GetActiveWorkstationsAsync(ct);
            var dist = new Dictionary<string, int>();

            foreach (var ws in workstations)
            {
                string ver = string.IsNullOrEmpty(ws.Version) ? "1.0.0" : ws.Version;
                if (dist.TryGetValue(ver, out int count))
                {
                    dist[ver] = count + 1;
                }
                else
                {
                    dist[ver] = 1;
                }
            }

            return new FleetVersionSummary
            {
                LatestClientVersion = "1.0.0",
                ClientVersionDistribution = dist
            };
        }

        public Task<FleetInventorySummary> GetFleetInventorySummaryAsync(CancellationToken ct = default)
        {
            var summary = new FleetInventorySummary
            {
                TotalGamesInstalled = 15,
                TopSoftwareInstalled = new Dictionary<string, int>
                {
                    { "GTA V", 12 },
                    { "Dota 2", 8 },
                    { "Counter-Strike 2", 15 }
                }
            };
            return Task.FromResult(summary);
        }

        public Task<FleetResourceUsageSummary> GetFleetResourceUsageSummaryAsync(CancellationToken ct = default)
        {
            var summary = new FleetResourceUsageSummary
            {
                AverageCpuUsagePercent = 25.5,
                AverageRamUsagePercent = 42.0,
                AverageGpuUsagePercent = 38.5,
                AverageGpuTempCelsius = 55.0,
                AverageCpuTempCelsius = 52.5
            };
            return Task.FromResult(summary);
        }
    }
}
