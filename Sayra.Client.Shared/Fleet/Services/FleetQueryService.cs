using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Highly optimized query and statistics compilation service implementing <see cref="IFleetQueryService"/>.
    /// </summary>
    public class FleetQueryService : IFleetQueryService
    {
        private readonly IFleetDatabaseContext _dbContext;
        private readonly ILogger<FleetQueryService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FleetQueryService"/> class.
        /// </summary>
        public FleetQueryService(IFleetDatabaseContext dbContext, ILogger<FleetQueryService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<FleetStatistics> GetFleetStatisticsAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Compiling high-level fleet statistics...");

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            var stats = new FleetStatistics();

            // 1. Core Machine counts & status aggregation
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT
                        COUNT(*),
                        SUM(CASE WHEN Status = 'Online' THEN 1 ELSE 0 END),
                        SUM(CASE WHEN Status = 'Offline' THEN 1 ELSE 0 END),
                        SUM(CASE WHEN Status = 'InSession' THEN 1 ELSE 0 END),
                        SUM(CASE WHEN Status = 'Maintenance' THEN 1 ELSE 0 END),
                        SUM(CASE WHEN Status = 'Locked' THEN 1 ELSE 0 END)
                    FROM Workstations;";

                using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    stats.TotalMachinesCount = reader.GetInt32(0);
                    stats.OnlineCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    stats.OfflineCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    stats.InSessionCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    stats.MaintenanceCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                    stats.LockedCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                }
            }

            // 2. Health score averages & tier distribution counts
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT
                        AVG(OverallHealthScore),
                        SUM(CASE WHEN OverallHealthScore >= 90.0 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN OverallHealthScore >= 70.0 AND OverallHealthScore < 90.0 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN OverallHealthScore >= 50.0 AND OverallHealthScore < 70.0 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN OverallHealthScore < 50.0 THEN 1 ELSE 0 END)
                    FROM Health;";

                using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    stats.AverageHealthScore = reader.IsDBNull(0) ? 100.0 : reader.GetDouble(0);
                    stats.HealthyCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    stats.WarningCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    stats.CriticalCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    stats.EmergencyCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                }
            }

            // 3. Hardware aggregates (RAM) & OS distributions
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT SUM(RamGb) FROM Inventory;";
                var sumRam = await cmd.ExecuteScalarAsync(ct);
                stats.TotalRamGb = sumRam == DBNull.Value ? 0 : Convert.ToInt64(sumRam);
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT OperatingSystem, COUNT(*) FROM Inventory GROUP BY OperatingSystem;";
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    string osName = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
                    int count = reader.GetInt32(1);
                    stats.OSVersionCounts[osName] = count;
                }
            }

            return stats;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MachineInfo>> QueryByGroupAsync(string groupId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(groupId)) return Array.Empty<MachineInfo>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT w.MachineId, w.Hostname, w.IpAddress, w.MacAddress, w.Status, w.HealthStatus, w.LastSeenUtc,
                       w.SemVer, w.BuildHash, w.BuildDate,
                       i.CpuName, i.GpuName, i.RamGb, i.OperatingSystem, i.StorageDrivesJson
                FROM Workstations w
                JOIN GroupMembership gm ON w.MachineId = gm.MachineId
                LEFT JOIN Inventory i ON w.MachineId = i.MachineId
                WHERE gm.GroupId = $groupId;";
            cmd.Parameters.Add(new SqliteParameter("$groupId", groupId));

            var list = new List<MachineInfo>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadMachine(reader));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MachineInfo>> QueryByRegionAsync(string regionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(regionId)) return Array.Empty<MachineInfo>();

            // Region hierarchy is bound via tags of Key='Region' and Value=regionId
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT w.MachineId, w.Hostname, w.IpAddress, w.MacAddress, w.Status, w.HealthStatus, w.LastSeenUtc,
                       w.SemVer, w.BuildHash, w.BuildDate,
                       i.CpuName, i.GpuName, i.RamGb, i.OperatingSystem, i.StorageDrivesJson
                FROM Workstations w
                JOIN Tags t ON w.MachineId = t.MachineId
                LEFT JOIN Inventory i ON w.MachineId = i.MachineId
                WHERE t.Key = 'Region' AND t.Value = $regionId;";
            cmd.Parameters.Add(new SqliteParameter("$regionId", regionId));

            var list = new List<MachineInfo>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadMachine(reader));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MachineInfo>> QueryByDepartmentAsync(string departmentId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(departmentId)) return Array.Empty<MachineInfo>();

            // Department division is bound via tags of Key='Department' and Value=departmentId
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT w.MachineId, w.Hostname, w.IpAddress, w.MacAddress, w.Status, w.HealthStatus, w.LastSeenUtc,
                       w.SemVer, w.BuildHash, w.BuildDate,
                       i.CpuName, i.GpuName, i.RamGb, i.OperatingSystem, i.StorageDrivesJson
                FROM Workstations w
                JOIN Tags t ON w.MachineId = t.MachineId
                LEFT JOIN Inventory i ON w.MachineId = i.MachineId
                WHERE t.Key = 'Department' AND t.Value = $deptId;";
            cmd.Parameters.Add(new SqliteParameter("$deptId", departmentId));

            var list = new List<MachineInfo>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadMachine(reader));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MachineInfo>> QueryByStatusAsync(string status, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(status)) return Array.Empty<MachineInfo>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT w.MachineId, w.Hostname, w.IpAddress, w.MacAddress, w.Status, w.HealthStatus, w.LastSeenUtc,
                       w.SemVer, w.BuildHash, w.BuildDate,
                       i.CpuName, i.GpuName, i.RamGb, i.OperatingSystem, i.StorageDrivesJson
                FROM Workstations w
                LEFT JOIN Inventory i ON w.MachineId = i.MachineId
                WHERE w.Status = $status;";
            cmd.Parameters.Add(new SqliteParameter("$status", status));

            var list = new List<MachineInfo>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadMachine(reader));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MachineInfo>> QueryByHealthStatusAsync(string healthStatus, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(healthStatus)) return Array.Empty<MachineInfo>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT w.MachineId, w.Hostname, w.IpAddress, w.MacAddress, w.Status, w.HealthStatus, w.LastSeenUtc,
                       w.SemVer, w.BuildHash, w.BuildDate,
                       i.CpuName, i.GpuName, i.RamGb, i.OperatingSystem, i.StorageDrivesJson
                FROM Workstations w
                LEFT JOIN Inventory i ON w.MachineId = i.MachineId
                WHERE w.HealthStatus = $hStatus;";
            cmd.Parameters.Add(new SqliteParameter("$hStatus", healthStatus));

            var list = new List<MachineInfo>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadMachine(reader));
            }

            return list;
        }

        private static MachineInfo ReadMachine(DbDataReader reader)
        {
            string machineId = reader.GetString(0);
            string hostname = reader.GetString(1);
            string ipAddress = reader.GetString(2);
            string macAddress = reader.GetString(3);

            Enum.TryParse<MachineStatus>(reader.GetString(4), out var status);
            Enum.TryParse<MachineHealthStatus>(reader.GetString(5), out var healthStatus);

            DateTime lastSeenUtc = reader.GetDateTime(6);

            var version = new MachineVersion
            {
                SemVer = reader.GetString(7),
                BuildHash = reader.GetString(8),
                BuildDate = reader.GetDateTime(9)
            };

            var inventory = new MachineInventory();
            if (!reader.IsDBNull(10))
            {
                var drivesJson = reader.GetString(14);
                var drives = JsonSerializer.Deserialize<Dictionary<string, string>>(drivesJson) ?? new Dictionary<string, string>();

                inventory = new MachineInventory
                {
                    CpuName = reader.GetString(10),
                    GpuName = reader.GetString(11),
                    RamGb = reader.GetInt32(12),
                    OperatingSystem = reader.GetString(13),
                    StorageDrives = drives
                };
            }

            return new MachineInfo
            {
                MachineId = machineId,
                Hostname = hostname,
                IpAddress = ipAddress,
                MacAddress = macAddress,
                Status = status,
                HealthStatus = healthStatus,
                LastSeenUtc = lastSeenUtc,
                Version = version,
                Inventory = inventory
            };
        }
    }
}
