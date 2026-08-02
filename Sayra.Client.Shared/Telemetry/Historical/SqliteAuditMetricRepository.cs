using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Historical
{
    /// <summary>
    /// SQLite implementation of Audit Metric repository.
    /// </summary>
    public class SqliteAuditMetricRepository : IAuditMetricRepository
    {
        private readonly IHistoricalStorageProvider _storageProvider;

        public SqliteAuditMetricRepository(IHistoricalStorageProvider storageProvider)
        {
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        }

        public async Task InsertAsync(AuditMetric record, CancellationToken cancellationToken = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            var sql = @"
                INSERT OR REPLACE INTO AuditMetrics (
                    AuditId, Timestamp, Name, MachineId, SessionId, UserId, OperatorId, Details, Count, DurationMs
                ) VALUES (
                    $AuditId, $Timestamp, $Name, $MachineId, $SessionId, $UserId, $OperatorId, $Details, $Count, $DurationMs
                );";

            var parameters = MapParameters(record);
            await _storageProvider.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        }

        public async Task BatchInsertAsync(IEnumerable<AuditMetric> records, CancellationToken cancellationToken = default)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));

            var sql = @"
                INSERT OR REPLACE INTO AuditMetrics (
                    AuditId, Timestamp, Name, MachineId, SessionId, UserId, OperatorId, Details, Count, DurationMs
                ) VALUES (
                    $AuditId, $Timestamp, $Name, $MachineId, $SessionId, $UserId, $OperatorId, $Details, $Count, $DurationMs
                );";

            var batchParams = new List<Dictionary<string, object?>>();
            foreach (var record in records)
            {
                batchParams.Add(MapParameters(record));
            }

            if (batchParams.Count > 0)
            {
                await _storageProvider.ExecuteBatchAsync(sql, batchParams, cancellationToken);
            }
        }

        public async Task<IReadOnlyCollection<AuditMetric>> QueryAsync(
            DateTime? start = null,
            DateTime? end = null,
            string? name = null,
            string? machineId = null,
            string? sessionId = null,
            CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM AuditMetrics WHERE 1=1";
            var parameters = new Dictionary<string, object?>();

            if (start.HasValue)
            {
                sql += " AND Timestamp >= $Start";
                parameters["$Start"] = start.Value.ToString("O");
            }
            if (end.HasValue)
            {
                sql += " AND Timestamp <= $End";
                parameters["$End"] = end.Value.ToString("O");
            }
            if (!string.IsNullOrEmpty(name))
            {
                sql += " AND Name = $Name";
                parameters["$Name"] = name;
            }
            if (!string.IsNullOrEmpty(machineId))
            {
                sql += " AND MachineId = $MachineId";
                parameters["$MachineId"] = machineId;
            }
            if (!string.IsNullOrEmpty(sessionId))
            {
                sql += " AND SessionId = $SessionId";
                parameters["$SessionId"] = sessionId;
            }

            sql += " ORDER BY Timestamp DESC;";

            return await _storageProvider.QueryAsync(sql, parameters, ReadRecord, cancellationToken);
        }

        public async Task DeleteAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
        {
            var sql = "DELETE FROM AuditMetrics WHERE Timestamp < $Before;";
            var parameters = new Dictionary<string, object?>
            {
                ["$Before"] = beforeUtc.ToString("O")
            };
            await _storageProvider.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        }

        private static Dictionary<string, object?> MapParameters(AuditMetric a)
        {
            return new Dictionary<string, object?>
            {
                ["$AuditId"] = a.AuditId,
                ["$Timestamp"] = a.Timestamp.ToString("O"),
                ["$Name"] = a.Name,
                ["$MachineId"] = a.MachineId,
                ["$SessionId"] = a.SessionId,
                ["$UserId"] = a.UserId,
                ["$OperatorId"] = a.OperatorId,
                ["$Details"] = a.Details,
                ["$Count"] = a.Count,
                ["$DurationMs"] = (long)a.Duration.TotalMilliseconds
            };
        }

        private static AuditMetric ReadRecord(IDataRecord r)
        {
            return new AuditMetric
            {
                AuditId = r.GetString(r.GetOrdinal("AuditId")),
                Timestamp = DateTime.Parse(r.GetString(r.GetOrdinal("Timestamp"))),
                Name = r.GetString(r.GetOrdinal("Name")),
                MachineId = r.GetString(r.GetOrdinal("MachineId")),
                SessionId = r.IsDBNull(r.GetOrdinal("SessionId")) ? null : r.GetString(r.GetOrdinal("SessionId")),
                UserId = r.IsDBNull(r.GetOrdinal("UserId")) ? null : r.GetString(r.GetOrdinal("UserId")),
                OperatorId = r.IsDBNull(r.GetOrdinal("OperatorId")) ? null : r.GetString(r.GetOrdinal("OperatorId")),
                Details = r.GetString(r.GetOrdinal("Details")),
                Count = r.GetInt64(r.GetOrdinal("Count")),
                Duration = TimeSpan.FromMilliseconds(r.GetInt64(r.GetOrdinal("DurationMs")))
            };
        }
    }
}
