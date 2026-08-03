using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Infrastructure
{
    /// <summary>
    /// Highly secure SQLCipher implementation of <see cref="IMachineRepository"/>.
    /// </summary>
    public class MachineRepository : IMachineRepository
    {
        private readonly IFleetDatabaseContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="MachineRepository"/> class.
        /// </summary>
        public MachineRepository(IFleetDatabaseContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<bool> SaveAsync(MachineInfo machine, CancellationToken ct = default)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                // 1. Save core machine details
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Workstations (MachineId, Hostname, IpAddress, MacAddress, Status, HealthStatus, LastSeenUtc, SemVer, BuildHash, BuildDate)
                        VALUES ($id, $hostname, $ip, $mac, $status, $healthStatus, $lastSeen, $semVer, $buildHash, $buildDate)
                        ON CONFLICT(MachineId) DO UPDATE SET
                            Hostname = excluded.Hostname,
                            IpAddress = excluded.IpAddress,
                            MacAddress = excluded.MacAddress,
                            Status = excluded.Status,
                            HealthStatus = excluded.HealthStatus,
                            LastSeenUtc = excluded.LastSeenUtc,
                            SemVer = excluded.SemVer,
                            BuildHash = excluded.BuildHash,
                            BuildDate = excluded.BuildDate;";

                    cmd.Parameters.Add(new SqliteParameter("$id", machine.MachineId));
                    cmd.Parameters.Add(new SqliteParameter("$hostname", machine.Hostname));
                    cmd.Parameters.Add(new SqliteParameter("$ip", machine.IpAddress));
                    cmd.Parameters.Add(new SqliteParameter("$mac", machine.MacAddress));
                    cmd.Parameters.Add(new SqliteParameter("$status", machine.Status.ToString()));
                    cmd.Parameters.Add(new SqliteParameter("$healthStatus", machine.HealthStatus.ToString()));
                    cmd.Parameters.Add(new SqliteParameter("$lastSeen", machine.LastSeenUtc.ToString("O")));
                    cmd.Parameters.Add(new SqliteParameter("$semVer", machine.Version.SemVer));
                    cmd.Parameters.Add(new SqliteParameter("$buildHash", machine.Version.BuildHash));
                    cmd.Parameters.Add(new SqliteParameter("$buildDate", machine.Version.BuildDate.ToString("O")));

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // 2. Save inventory details (if any)
                if (machine.Inventory != null)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Inventory (MachineId, CpuName, GpuName, RamGb, OperatingSystem, StorageDrivesJson)
                        VALUES ($id, $cpu, $gpu, $ram, $os, $drives)
                        ON CONFLICT(MachineId) DO UPDATE SET
                            CpuName = excluded.CpuName,
                            GpuName = excluded.GpuName,
                            RamGb = excluded.RamGb,
                            OperatingSystem = excluded.OperatingSystem,
                            StorageDrivesJson = excluded.StorageDrivesJson;";

                    string drivesJson = JsonSerializer.Serialize(machine.Inventory.StorageDrives ?? new Dictionary<string, string>());

                    cmd.Parameters.Add(new SqliteParameter("$id", machine.MachineId));
                    cmd.Parameters.Add(new SqliteParameter("$cpu", machine.Inventory.CpuName ?? string.Empty));
                    cmd.Parameters.Add(new SqliteParameter("$gpu", machine.Inventory.GpuName ?? string.Empty));
                    cmd.Parameters.Add(new SqliteParameter("$ram", machine.Inventory.RamGb));
                    cmd.Parameters.Add(new SqliteParameter("$os", machine.Inventory.OperatingSystem ?? string.Empty));
                    cmd.Parameters.Add(new SqliteParameter("$drives", drivesJson));

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM Workstations WHERE MachineId = $id;";
                    cmd.Parameters.Add(new SqliteParameter("$id", machineId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM Inventory WHERE MachineId = $id;";
                    cmd.Parameters.Add(new SqliteParameter("$id", machineId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM Snapshots WHERE MachineId = $id;";
                    cmd.Parameters.Add(new SqliteParameter("$id", machineId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM Health WHERE MachineId = $id;";
                    cmd.Parameters.Add(new SqliteParameter("$id", machineId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM Tags WHERE MachineId = $id;";
                    cmd.Parameters.Add(new SqliteParameter("$id", machineId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM GroupMembership WHERE MachineId = $id;";
                    cmd.Parameters.Add(new SqliteParameter("$id", machineId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<MachineInfo?> GetAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT w.MachineId, w.Hostname, w.IpAddress, w.MacAddress, w.Status, w.HealthStatus, w.LastSeenUtc,
                       w.SemVer, w.BuildHash, w.BuildDate,
                       i.CpuName, i.GpuName, i.RamGb, i.OperatingSystem, i.StorageDrivesJson
                FROM Workstations w
                LEFT JOIN Inventory i ON w.MachineId = i.MachineId
                WHERE w.MachineId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", machineId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return ReadMachine(reader);
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MachineInfo>> GetAllAsync(CancellationToken ct = default)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT w.MachineId, w.Hostname, w.IpAddress, w.MacAddress, w.Status, w.HealthStatus, w.LastSeenUtc,
                       w.SemVer, w.BuildHash, w.BuildDate,
                       i.CpuName, i.GpuName, i.RamGb, i.OperatingSystem, i.StorageDrivesJson
                FROM Workstations w
                LEFT JOIN Inventory i ON w.MachineId = i.MachineId;";

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
