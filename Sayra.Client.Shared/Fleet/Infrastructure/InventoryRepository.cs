using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Infrastructure
{
    /// <summary>
    /// Highly secure SQLCipher implementation of <see cref="IInventoryRepository"/>.
    /// </summary>
    public class InventoryRepository : IInventoryRepository
    {
        private readonly IFleetDatabaseContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="InventoryRepository"/> class.
        /// </summary>
        public InventoryRepository(IFleetDatabaseContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<bool> SaveAsync(string machineId, MachineInventory inventory, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || inventory == null) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Inventory (MachineId, CpuName, GpuName, RamGb, OperatingSystem, StorageDrivesJson)
                VALUES ($id, $cpu, $gpu, $ram, $os, $drives)
                ON CONFLICT(MachineId) DO UPDATE SET
                    CpuName = excluded.CpuName,
                    GpuName = excluded.GpuName,
                    RamGb = excluded.RamGb,
                    OperatingSystem = excluded.OperatingSystem,
                    StorageDrivesJson = excluded.StorageDrivesJson;";

            string drivesJson = JsonSerializer.Serialize(inventory.StorageDrives ?? new Dictionary<string, string>());

            cmd.Parameters.Add(new SqliteParameter("$id", machineId));
            cmd.Parameters.Add(new SqliteParameter("$cpu", inventory.CpuName ?? string.Empty));
            cmd.Parameters.Add(new SqliteParameter("$gpu", inventory.GpuName ?? string.Empty));
            cmd.Parameters.Add(new SqliteParameter("$ram", inventory.RamGb));
            cmd.Parameters.Add(new SqliteParameter("$os", inventory.OperatingSystem ?? string.Empty));
            cmd.Parameters.Add(new SqliteParameter("$drives", drivesJson));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<MachineInventory?> GetAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT CpuName, GpuName, RamGb, OperatingSystem, StorageDrivesJson FROM Inventory WHERE MachineId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", machineId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var drivesJson = reader.GetString(4);
                var drives = JsonSerializer.Deserialize<Dictionary<string, string>>(drivesJson) ?? new Dictionary<string, string>();

                return new MachineInventory
                {
                    CpuName = reader.GetString(0),
                    GpuName = reader.GetString(1),
                    RamGb = reader.GetInt32(2),
                    OperatingSystem = reader.GetString(3),
                    StorageDrives = drives
                };
            }

            return null;
        }
    }
}
