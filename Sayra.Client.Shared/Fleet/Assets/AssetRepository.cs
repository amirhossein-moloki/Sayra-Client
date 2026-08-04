using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sayra.Client.Shared.Fleet.Assets.Interfaces;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Assets
{
    /// <summary>
    /// SQLCipher-secured SQLite implementation of <see cref="IAssetRepository"/>.
    /// </summary>
    public class AssetRepository : IAssetRepository
    {
        private readonly IFleetDatabaseContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssetRepository"/> class.
        /// </summary>
        public AssetRepository(IFleetDatabaseContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<bool> SaveAssetAsync(AssetRecord asset, CancellationToken ct = default)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Assets (AssetId, MachineId, Name, SerialOrSignature, Category, Status, SpecificationsJson, Manufacturer, Version, DriverVersion, SoftwareName)
                VALUES ($assetId, $machineId, $name, $serial, $category, $status, $specJson, $manufacturer, $version, $driverVersion, $softwareName)
                ON CONFLICT(AssetId) DO UPDATE SET
                    MachineId = excluded.MachineId,
                    Name = excluded.Name,
                    SerialOrSignature = excluded.SerialOrSignature,
                    Category = excluded.Category,
                    Status = excluded.Status,
                    SpecificationsJson = excluded.SpecificationsJson,
                    Manufacturer = excluded.Manufacturer,
                    Version = excluded.Version,
                    DriverVersion = excluded.DriverVersion,
                    SoftwareName = excluded.SoftwareName;";

            string specsJson = JsonSerializer.Serialize(asset.Specifications ?? new Dictionary<string, string>());
            string manufacturer = asset.Specifications?.GetValueOrDefault("Manufacturer") ?? string.Empty;
            string version = asset.Specifications?.GetValueOrDefault("Version") ?? string.Empty;
            string driverVersion = asset.Specifications?.GetValueOrDefault("DriverVersion") ?? string.Empty;
            string softwareName = asset.Specifications?.GetValueOrDefault("SoftwareName") ?? (asset.Category == AssetType.Software ? asset.Name : string.Empty);

            cmd.Parameters.Add(new SqliteParameter("$assetId", asset.AssetId));
            cmd.Parameters.Add(new SqliteParameter("$machineId", asset.MachineId));
            cmd.Parameters.Add(new SqliteParameter("$name", asset.Name));
            cmd.Parameters.Add(new SqliteParameter("$serial", asset.SerialOrSignature));
            cmd.Parameters.Add(new SqliteParameter("$category", asset.Category.ToString()));
            cmd.Parameters.Add(new SqliteParameter("$status", asset.Status.ToString()));
            cmd.Parameters.Add(new SqliteParameter("$specJson", specsJson));
            cmd.Parameters.Add(new SqliteParameter("$manufacturer", manufacturer));
            cmd.Parameters.Add(new SqliteParameter("$version", version));
            cmd.Parameters.Add(new SqliteParameter("$driverVersion", driverVersion));
            cmd.Parameters.Add(new SqliteParameter("$softwareName", softwareName));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<AssetRecord?> GetAssetAsync(string assetId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(assetId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT AssetId, MachineId, Name, SerialOrSignature, Category, Status, SpecificationsJson
                FROM Assets
                WHERE AssetId = $assetId;";
            cmd.Parameters.Add(new SqliteParameter("$assetId", assetId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return ReadAsset(reader);
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAssetAsync(string assetId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(assetId)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Assets WHERE AssetId = $assetId;";
            cmd.Parameters.Add(new SqliteParameter("$assetId", assetId));

            int rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AssetRecord>> GetAssetsByMachineAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return Array.Empty<AssetRecord>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT AssetId, MachineId, Name, SerialOrSignature, Category, Status, SpecificationsJson
                FROM Assets
                WHERE MachineId = $machineId;";
            cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));

            var list = new List<AssetRecord>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadAsset(reader));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<bool> SaveInventorySnapshotAsync(string machineId, IEnumerable<AssetRecord> assets, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                // Delete existing assets for machine first to represent latest snapshot
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM Assets WHERE MachineId = $machineId;";
                    cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // Insert new ones
                foreach (var asset in assets)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Assets (AssetId, MachineId, Name, SerialOrSignature, Category, Status, SpecificationsJson, Manufacturer, Version, DriverVersion, SoftwareName)
                        VALUES ($assetId, $machineId, $name, $serial, $category, $status, $specJson, $manufacturer, $version, $driverVersion, $softwareName);";

                    string specsJson = JsonSerializer.Serialize(asset.Specifications ?? new Dictionary<string, string>());
                    string manufacturer = asset.Specifications?.GetValueOrDefault("Manufacturer") ?? string.Empty;
                    string version = asset.Specifications?.GetValueOrDefault("Version") ?? string.Empty;
                    string driverVersion = asset.Specifications?.GetValueOrDefault("DriverVersion") ?? string.Empty;
                    string softwareName = asset.Specifications?.GetValueOrDefault("SoftwareName") ?? (asset.Category == AssetType.Software ? asset.Name : string.Empty);

                    cmd.Parameters.Add(new SqliteParameter("$assetId", asset.AssetId));
                    cmd.Parameters.Add(new SqliteParameter("$machineId", asset.MachineId));
                    cmd.Parameters.Add(new SqliteParameter("$name", asset.Name));
                    cmd.Parameters.Add(new SqliteParameter("$serial", asset.SerialOrSignature));
                    cmd.Parameters.Add(new SqliteParameter("$category", asset.Category.ToString()));
                    cmd.Parameters.Add(new SqliteParameter("$status", asset.Status.ToString()));
                    cmd.Parameters.Add(new SqliteParameter("$specJson", specsJson));
                    cmd.Parameters.Add(new SqliteParameter("$manufacturer", manufacturer));
                    cmd.Parameters.Add(new SqliteParameter("$version", version));
                    cmd.Parameters.Add(new SqliteParameter("$driverVersion", driverVersion));
                    cmd.Parameters.Add(new SqliteParameter("$softwareName", softwareName));

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
        public async Task<bool> RecordHistoryAsync(AssetHistory history, CancellationToken ct = default)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AssetHistory (HistoryId, AssetId, MachineId, TimestampUtc, EventType, Description, OperatorId)
                VALUES ($historyId, $assetId, $machineId, $timestamp, $eventType, $description, $operatorId);";

            cmd.Parameters.Add(new SqliteParameter("$historyId", history.HistoryId));
            cmd.Parameters.Add(new SqliteParameter("$assetId", history.AssetId));
            cmd.Parameters.Add(new SqliteParameter("$machineId", history.MachineId));
            cmd.Parameters.Add(new SqliteParameter("$timestamp", history.TimestampUtc.ToString("O")));
            cmd.Parameters.Add(new SqliteParameter("$eventType", history.EventType));
            cmd.Parameters.Add(new SqliteParameter("$description", history.Description));
            cmd.Parameters.Add(new SqliteParameter("$operatorId", history.OperatorId));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AssetHistory>> GetHistoryAsync(string? assetId = null, string? machineId = null, CancellationToken ct = default)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            var query = new StringBuilder("SELECT HistoryId, AssetId, MachineId, TimestampUtc, EventType, Description, OperatorId FROM AssetHistory WHERE 1=1");

            if (!string.IsNullOrEmpty(assetId))
            {
                query.Append(" AND AssetId = $assetId");
                cmd.Parameters.Add(new SqliteParameter("$assetId", assetId));
            }
            if (!string.IsNullOrEmpty(machineId))
            {
                query.Append(" AND MachineId = $machineId");
                cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));
            }

            query.Append(" ORDER BY TimestampUtc DESC;");
            cmd.CommandText = query.ToString();

            var list = new List<AssetHistory>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new AssetHistory
                {
                    HistoryId = reader.GetString(0),
                    AssetId = reader.GetString(1),
                    MachineId = reader.GetString(2),
                    TimestampUtc = DateTime.Parse(reader.GetString(3)),
                    EventType = reader.GetString(4),
                    Description = reader.GetString(5),
                    OperatorId = reader.GetString(6)
                });
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<bool> RecordChangeAsync(AssetChangeRecord change, CancellationToken ct = default)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AssetChanges (ChangeId, AssetId, MachineId, TimestampUtc, ChangeType, PropertyName, OldValue, NewValue)
                VALUES ($changeId, $assetId, $machineId, $timestamp, $changeType, $propertyName, $oldValue, $newValue);";

            cmd.Parameters.Add(new SqliteParameter("$changeId", change.ChangeId));
            cmd.Parameters.Add(new SqliteParameter("$assetId", change.AssetId));
            cmd.Parameters.Add(new SqliteParameter("$machineId", change.MachineId));
            cmd.Parameters.Add(new SqliteParameter("$timestamp", change.TimestampUtc.ToString("O")));
            cmd.Parameters.Add(new SqliteParameter("$changeType", change.ChangeType));
            cmd.Parameters.Add(new SqliteParameter("$propertyName", change.PropertyName));
            cmd.Parameters.Add(new SqliteParameter("$oldValue", change.OldValue));
            cmd.Parameters.Add(new SqliteParameter("$newValue", change.NewValue));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AssetChangeRecord>> GetChangesAsync(string? assetId = null, string? machineId = null, CancellationToken ct = default)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            var query = new StringBuilder("SELECT ChangeId, AssetId, MachineId, TimestampUtc, ChangeType, PropertyName, OldValue, NewValue FROM AssetChanges WHERE 1=1");

            if (!string.IsNullOrEmpty(assetId))
            {
                query.Append(" AND AssetId = $assetId");
                cmd.Parameters.Add(new SqliteParameter("$assetId", assetId));
            }
            if (!string.IsNullOrEmpty(machineId))
            {
                query.Append(" AND MachineId = $machineId");
                cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));
            }

            query.Append(" ORDER BY TimestampUtc DESC;");
            cmd.CommandText = query.ToString();

            var list = new List<AssetChangeRecord>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new AssetChangeRecord
                {
                    ChangeId = reader.GetString(0),
                    AssetId = reader.GetString(1),
                    MachineId = reader.GetString(2),
                    TimestampUtc = DateTime.Parse(reader.GetString(3)),
                    ChangeType = reader.GetString(4),
                    PropertyName = reader.GetString(5),
                    OldValue = reader.GetString(6),
                    NewValue = reader.GetString(7)
                });
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<(IReadOnlyList<AssetRecord> Items, int TotalCount)> SearchAssetsAsync(
            string? machineId = null,
            string? assetType = null,
            string? serialNumber = null,
            string? version = null,
            string? manufacturer = null,
            string? driverVersion = null,
            string? softwareName = null,
            string? searchTerm = null,
            string? sortBy = null,
            bool ascending = true,
            int pageIndex = 0,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            var filterBuilder = new StringBuilder(" WHERE 1=1");
            var paramsList = new List<SqliteParameter>();

            if (!string.IsNullOrEmpty(machineId))
            {
                filterBuilder.Append(" AND MachineId = $machineId");
                paramsList.Add(new SqliteParameter("$machineId", machineId));
            }
            if (!string.IsNullOrEmpty(assetType))
            {
                filterBuilder.Append(" AND Category = $assetType");
                paramsList.Add(new SqliteParameter("$assetType", assetType));
            }
            if (!string.IsNullOrEmpty(serialNumber))
            {
                filterBuilder.Append(" AND SerialOrSignature = $serialNumber");
                paramsList.Add(new SqliteParameter("$serialNumber", serialNumber));
            }
            if (!string.IsNullOrEmpty(version))
            {
                filterBuilder.Append(" AND Version = $version");
                paramsList.Add(new SqliteParameter("$version", version));
            }
            if (!string.IsNullOrEmpty(manufacturer))
            {
                filterBuilder.Append(" AND Manufacturer LIKE $manufacturer");
                paramsList.Add(new SqliteParameter("$manufacturer", $"%{manufacturer}%"));
            }
            if (!string.IsNullOrEmpty(driverVersion))
            {
                filterBuilder.Append(" AND DriverVersion = $driverVersion");
                paramsList.Add(new SqliteParameter("$driverVersion", driverVersion));
            }
            if (!string.IsNullOrEmpty(softwareName))
            {
                filterBuilder.Append(" AND SoftwareName LIKE $softwareName");
                paramsList.Add(new SqliteParameter("$softwareName", $"%{softwareName}%"));
            }
            if (!string.IsNullOrEmpty(searchTerm))
            {
                filterBuilder.Append(" AND (Name LIKE $search OR SerialOrSignature LIKE $search OR Manufacturer LIKE $search OR SoftwareName LIKE $search)");
                paramsList.Add(new SqliteParameter("$search", $"%{searchTerm}%"));
            }

            // Get total count
            int totalCount = 0;
            using (var countCmd = connection.CreateCommand())
            {
                countCmd.CommandText = "SELECT COUNT(*) FROM Assets" + filterBuilder.ToString() + ";";
                foreach (var p in paramsList)
                {
                    countCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
                }
                totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));
            }

            // Sorting
            string sortCol = "AssetId";
            if (!string.IsNullOrEmpty(sortBy))
            {
                switch (sortBy.ToLowerInvariant())
                {
                    case "name": sortCol = "Name"; break;
                    case "machineid": sortCol = "MachineId"; break;
                    case "category": sortCol = "Category"; break;
                    case "status": sortCol = "Status"; break;
                    case "serialnumber": sortCol = "SerialOrSignature"; break;
                    case "manufacturer": sortCol = "Manufacturer"; break;
                    case "version": sortCol = "Version"; break;
                }
            }
            string direction = ascending ? "ASC" : "DESC";

            // Query items
            var list = new List<AssetRecord>();
            using (var itemsCmd = connection.CreateCommand())
            {
                itemsCmd.CommandText = $@"
                    SELECT AssetId, MachineId, Name, SerialOrSignature, Category, Status, SpecificationsJson
                    FROM Assets
                    {filterBuilder}
                    ORDER BY {sortCol} {direction}
                    LIMIT $limit OFFSET $offset;";

                foreach (var p in paramsList)
                {
                    itemsCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
                }
                itemsCmd.Parameters.Add(new SqliteParameter("$limit", pageSize));
                itemsCmd.Parameters.Add(new SqliteParameter("$offset", pageIndex * pageSize));

                using var reader = await itemsCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    list.Add(ReadAsset(reader));
                }
            }

            return (list, totalCount);
        }

        private static AssetRecord ReadAsset(DbDataReader reader)
        {
            string assetId = reader.GetString(0);
            string machineId = reader.GetString(1);
            string name = reader.GetString(2);
            string serial = reader.GetString(3);

            Enum.TryParse<AssetType>(reader.GetString(4), true, out var category);
            Enum.TryParse<AssetStatus>(reader.GetString(5), true, out var status);

            var specsJson = reader.GetString(6);
            var specs = JsonSerializer.Deserialize<Dictionary<string, string>>(specsJson) ?? new Dictionary<string, string>();

            return new AssetRecord
            {
                AssetId = assetId,
                MachineId = machineId,
                Name = name,
                SerialOrSignature = serial,
                Category = category,
                Status = status,
                Specifications = specs
            };
        }
    }
}
