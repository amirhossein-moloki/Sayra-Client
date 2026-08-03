using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Infrastructure
{
    /// <summary>
    /// Highly secure SQLCipher implementation of <see cref="ITagRepository"/>.
    /// </summary>
    public class TagRepository : ITagRepository
    {
        private readonly IFleetDatabaseContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="TagRepository"/> class.
        /// </summary>
        public TagRepository(IFleetDatabaseContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<bool> AssignTagAsync(string machineId, FleetTag tag, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || tag == null) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Tags (Key, Value, MachineId)
                VALUES ($key, $value, $machineId)
                ON CONFLICT(Key, Value, MachineId) DO NOTHING;";

            cmd.Parameters.Add(new SqliteParameter("$key", tag.Key));
            cmd.Parameters.Add(new SqliteParameter("$value", tag.Value));
            cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveTagAsync(string machineId, string key, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(key)) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Tags WHERE MachineId = $machineId AND Key = $key;";
            cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));
            cmd.Parameters.Add(new SqliteParameter("$key", key));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FleetTag>> GetTagsForMachineAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return Array.Empty<FleetTag>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Key, Value FROM Tags WHERE MachineId = $machineId;";
            cmd.Parameters.Add(new SqliteParameter("$machineId", machineId));

            var list = new List<FleetTag>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new FleetTag { Key = reader.GetString(0), Value = reader.GetString(1) });
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> GetMachineIdsWithTagAsync(string key, string value, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return Array.Empty<string>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MachineId FROM Tags WHERE Key = $key AND Value = $value;";
            cmd.Parameters.Add(new SqliteParameter("$key", key));
            cmd.Parameters.Add(new SqliteParameter("$value", value));

            var list = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(reader.GetString(0));
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FleetTag>> GetAllTagsAsync(CancellationToken ct = default)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT Key, Value FROM Tags;";

            var list = new List<FleetTag>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new FleetTag { Key = reader.GetString(0), Value = reader.GetString(1) });
            }

            return list;
        }
    }
}
