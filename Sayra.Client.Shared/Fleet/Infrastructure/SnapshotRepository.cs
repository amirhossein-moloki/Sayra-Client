using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Infrastructure
{
    /// <summary>
    /// Highly secure SQLCipher implementation of <see cref="ISnapshotRepository"/>.
    /// </summary>
    public class SnapshotRepository : ISnapshotRepository
    {
        private readonly IFleetDatabaseContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRepository"/> class.
        /// </summary>
        public SnapshotRepository(IFleetDatabaseContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<bool> SaveAsync(MachineSnapshot snapshot, CancellationToken ct = default)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Snapshots (MachineId, CapturedAt, Connection, Compliance, ActiveSessionId)
                VALUES ($id, $captured, $connection, $compliance, $session)
                ON CONFLICT(MachineId) DO UPDATE SET
                    CapturedAt = excluded.CapturedAt,
                    Connection = excluded.Connection,
                    Compliance = excluded.Compliance,
                    ActiveSessionId = excluded.ActiveSessionId;";

            cmd.Parameters.Add(new SqliteParameter("$id", snapshot.MachineId));
            cmd.Parameters.Add(new SqliteParameter("$captured", snapshot.CapturedAt.ToString("O")));
            cmd.Parameters.Add(new SqliteParameter("$connection", snapshot.Connection.ToString()));
            cmd.Parameters.Add(new SqliteParameter("$compliance", snapshot.Compliance.ToString()));
            cmd.Parameters.Add(new SqliteParameter("$session", snapshot.ActiveSessionId ?? string.Empty));

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <inheritdoc />
        public async Task<MachineSnapshot?> GetAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return null;

            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MachineId, CapturedAt, Connection, Compliance, ActiveSessionId FROM Snapshots WHERE MachineId = $id;";
            cmd.Parameters.Add(new SqliteParameter("$id", machineId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                Enum.TryParse<ConnectionStatus>(reader.GetString(2), out var connectionStatus);
                Enum.TryParse<ComplianceStatus>(reader.GetString(3), out var complianceStatus);

                return new MachineSnapshot
                {
                    MachineId = reader.GetString(0),
                    CapturedAt = reader.GetDateTime(1),
                    Connection = connectionStatus,
                    Compliance = complianceStatus,
                    ActiveSessionId = reader.GetString(4)
                };
            }

            return null;
        }
    }
}
