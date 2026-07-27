using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.RemoteOperations.Services.Fleet
{
    public class EnterpriseOperationService : IEnterpriseOperationService
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly ILogger<EnterpriseOperationService> _logger;

        public EnterpriseOperationService(ILocalDatabaseService databaseService, ILogger<EnterpriseOperationService> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Dictionary<string, object>> GetFleetHealthSummaryAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    SUM(CASE WHEN Status = 'Online' THEN 1 ELSE 0 END) as OnlineCount,
                    SUM(CASE WHEN Status = 'Offline' THEN 1 ELSE 0 END) as OfflineCount,
                    SUM(CASE WHEN Status = 'Maintenance' THEN 1 ELSE 0 END) as MaintenanceCount,
                    COUNT(*) as TotalCount
                FROM Workstations;";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var result = new Dictionary<string, object>();
            if (await reader.ReadAsync(cancellationToken))
            {
                result["OnlineCount"] = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                result["OfflineCount"] = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                result["MaintenanceCount"] = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                result["TotalCount"] = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            }
            return result;
        }

        public async Task<Dictionary<string, object>> GetFleetDiagnosticsSummaryAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    SUM(CASE WHEN Severity = 'Critical' THEN 1 ELSE 0 END) as CriticalAlerts,
                    SUM(CASE WHEN Severity = 'Warning' THEN 1 ELSE 0 END) as WarningAlerts,
                    COUNT(*) as ActiveAlerts
                FROM FleetAlerts
                WHERE Status = 'Active';";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var result = new Dictionary<string, object>();
            if (await reader.ReadAsync(cancellationToken))
            {
                result["CriticalAlerts"] = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                result["WarningAlerts"] = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                result["ActiveAlerts"] = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            }
            return result;
        }

        public async Task<Dictionary<string, object>> GetFleetPolicyStatusAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    COUNT(*) as TotalPolicies,
                    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) as ActivePolicies
                FROM AppliedPolicies;";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var result = new Dictionary<string, object>();
            if (await reader.ReadAsync(cancellationToken))
            {
                result["TotalPolicies"] = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                result["ActivePolicies"] = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            }
            return result;
        }

        public async Task<Dictionary<string, object>> GetFleetSecurityStatusAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) as BlockedDevicesCount
                FROM FleetAlerts
                WHERE MetricName = 'USB_BLOCK' AND Status = 'Active';";

            var val = await cmd.ExecuteScalarAsync(cancellationToken);
            var result = new Dictionary<string, object>
            {
                ["BlockedDevicesCount"] = val == null || val == DBNull.Value ? 0 : Convert.ToInt32(val),
                ["SecurityStatus"] = "Hardened"
            };
            return result;
        }

        public async Task<Dictionary<string, object>> GetFleetVersionSummaryAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MetadataJson FROM Workstations;";

            var versions = new Dictionary<string, int>();
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    try
                    {
                        var meta = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(0));
                        if (meta != null && meta.TryGetValue("Version", out var ver))
                        {
                            versions[ver] = versions.TryGetValue(ver, out var count) ? count + 1 : 1;
                        }
                    }
                    catch { }
                }
            }

            return new Dictionary<string, object>
            {
                ["Versions"] = versions
            };
        }

        public async Task<Dictionary<string, object>> GetFleetInventorySummaryAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MetadataJson FROM Workstations;";

            int gamesInstalled = 0;
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    try
                    {
                        var meta = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(0));
                        if (meta != null && meta.TryGetValue("InstalledGames", out var countStr) && int.TryParse(countStr, out var count))
                        {
                            gamesInstalled += count;
                        }
                    }
                    catch { }
                }
            }

            return new Dictionary<string, object>
            {
                ["TotalGamesInstalled"] = gamesInstalled,
                ["InventoryStatus"] = "Synchronized"
            };
        }

        public async Task<Dictionary<string, object>> GetFleetResourceUsageSummaryAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MetadataJson FROM Workstations;";

            double cpuSum = 0;
            double ramSum = 0;
            double gpuSum = 0;
            int count = 0;

            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    try
                    {
                        var meta = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(0));
                        if (meta != null)
                        {
                            if (meta.TryGetValue("CpuUsage", out var cpu) && double.TryParse(cpu, out var cv)) cpuSum += cv;
                            if (meta.TryGetValue("RamUsage", out var ram) && double.TryParse(ram, out var rv)) ramSum += rv;
                            if (meta.TryGetValue("GpuUsage", out var gpu) && double.TryParse(gpu, out var gv)) gpuSum += gv;
                            count++;
                        }
                    }
                    catch { }
                }
            }

            return new Dictionary<string, object>
            {
                ["AvgCpuUsage"] = count > 0 ? cpuSum / count : 0,
                ["AvgRamUsage"] = count > 0 ? ramSum / count : 0,
                ["AvgGpuUsage"] = count > 0 ? gpuSum / count : 0,
                ["SampleCount"] = count
            };
        }
    }
}
