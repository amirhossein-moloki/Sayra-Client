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
    /// Highly secure SQLCipher implementation of <see cref="IHealthRepository"/>.
    /// </summary>
    public class HealthRepository : IHealthRepository
    {
        private readonly IFleetDatabaseContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthRepository"/> class.
        /// </summary>
        public HealthRepository(IFleetDatabaseContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<bool> SaveHealthAsync(MachineHealth health, CancellationToken ct = default)
        {
            if (health == null) throw new ArgumentNullException(nameof(health));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Health (MachineId, OverallHealthScore, ActiveWarningsCount, ActiveEmergenciesCount, SubsystemScoresJson)
                VALUES ($id, $score, $warnings, $emergencies, $scoresJson)
                ON CONFLICT(MachineId) DO UPDATE SET
                    OverallHealthScore = excluded.OverallHealthScore,
                    ActiveWarningsCount = excluded.ActiveWarningsCount,
                    ActiveEmergenciesCount = excluded.ActiveEmergenciesCount,
                    SubsystemScoresJson = excluded.SubsystemScoresJson;";

            string scoresJson = JsonSerializer.Serialize(health.SubsystemScores ?? new Dictionary<string, double>());

            cmd.Parameters.Add(new SqliteParameter("$id", health.MachineId));
            cmd.Parameters.Add(new SqliteParameter("$score", health.OverallHealthScore));
            cmd.Parameters.Add(new SqliteParameter("$warnings", health.ActiveWarningsCount));
            cmd.Parameters.Add(new SqliteParameter("$emergencies", health.ActiveEmergenciesCount));
            cmd.Parameters.Add(new SqliteParameter("$scoresJson", scoresJson));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<MachineHealth?> GetHealthAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MachineId, OverallHealthScore, ActiveWarningsCount, ActiveEmergenciesCount, SubsystemScoresJson FROM Health WHERE MachineId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", machineId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var scoresJson = reader.GetString(4);
                var scores = JsonSerializer.Deserialize<Dictionary<string, double>>(scoresJson) ?? new Dictionary<string, double>();

                return new MachineHealth
                {
                    MachineId = reader.GetString(0),
                    OverallHealthScore = reader.GetDouble(1),
                    ActiveWarningsCount = reader.GetInt32(2),
                    ActiveEmergenciesCount = reader.GetInt32(3),
                    SubsystemScores = scores
                };
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<bool> LogSnapshotAsync(string machineId, HealthSnapshot snapshot, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || snapshot == null) return false;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO HealthHistory (MachineId, TimestampUtc, CpuUtilization, MemoryUtilization, StorageUtilization, NetworkThroughput)
                VALUES ($id, $timestamp, $cpu, $mem, $storage, $net);";

            cmd.Parameters.Add(new SqliteParameter("$id", machineId));
            cmd.Parameters.Add(new SqliteParameter("$timestamp", snapshot.TimestampUtc.ToString("O")));
            cmd.Parameters.Add(new SqliteParameter("$cpu", snapshot.CpuUtilization));
            cmd.Parameters.Add(new SqliteParameter("$mem", snapshot.MemoryUtilization));
            cmd.Parameters.Add(new SqliteParameter("$storage", snapshot.StorageUtilization));
            cmd.Parameters.Add(new SqliteParameter("$net", snapshot.NetworkThroughputBytesPerSec));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<HealthSnapshot>> GetHistoryAsync(string machineId, int limit = 100, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return Array.Empty<HealthSnapshot>();

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT TimestampUtc, CpuUtilization, MemoryUtilization, StorageUtilization, NetworkThroughput
                FROM HealthHistory
                WHERE MachineId = $id
                ORDER BY TimestampUtc DESC
                LIMIT $limit;";

            cmd.Parameters.Add(new SqliteParameter("$id", machineId));
            cmd.Parameters.Add(new SqliteParameter("$limit", limit));

            var list = new List<HealthSnapshot>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new HealthSnapshot
                {
                    SnapshotId = Guid.NewGuid(),
                    TimestampUtc = reader.GetDateTime(0),
                    CpuUtilization = reader.GetDouble(1),
                    MemoryUtilization = reader.GetDouble(2),
                    StorageUtilization = reader.GetDouble(3),
                    NetworkThroughputBytesPerSec = reader.GetDouble(4)
                });
            }

            return list;
        }
    }
}
