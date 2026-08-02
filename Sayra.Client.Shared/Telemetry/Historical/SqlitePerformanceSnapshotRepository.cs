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
    /// SQLite implementation of Performance Snapshot repository.
    /// </summary>
    public class SqlitePerformanceSnapshotRepository : IPerformanceSnapshotRepository
    {
        private readonly IHistoricalStorageProvider _storageProvider;

        public SqlitePerformanceSnapshotRepository(IHistoricalStorageProvider storageProvider)
        {
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        }

        public async Task InsertAsync(PerformanceSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var sql = @"
                INSERT INTO PerformanceSnapshots (
                    Timestamp, StartupTimeMs, AuthenticationTimeMs, DatabaseLatencyMs, IpcLatencyMs, TcpLatencyMs,
                    DownloadSpeed, UploadSpeed, DiskLatencyMs, CacheHitRatio, QueueLength, WorkerExecutionTimeMs,
                    GarbageCollectionCount, ThreadPoolThreads, AsyncOperationsCount, MachineId, Subsystem,
                    Operation, Status, TraceId, CorrelationId, DurationMs
                ) VALUES (
                    $Timestamp, $StartupTimeMs, $AuthenticationTimeMs, $DatabaseLatencyMs, $IpcLatencyMs, $TcpLatencyMs,
                    $DownloadSpeed, $UploadSpeed, $DiskLatencyMs, $CacheHitRatio, $QueueLength, $WorkerExecutionTimeMs,
                    $GarbageCollectionCount, $ThreadPoolThreads, $AsyncOperationsCount, $MachineId, $Subsystem,
                    $Operation, $Status, $TraceId, $CorrelationId, $DurationMs
                );";

            var parameters = MapParameters(snapshot);
            await _storageProvider.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        }

        public async Task BatchInsertAsync(IEnumerable<PerformanceSnapshot> snapshots, CancellationToken cancellationToken = default)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));

            var sql = @"
                INSERT INTO PerformanceSnapshots (
                    Timestamp, StartupTimeMs, AuthenticationTimeMs, DatabaseLatencyMs, IpcLatencyMs, TcpLatencyMs,
                    DownloadSpeed, UploadSpeed, DiskLatencyMs, CacheHitRatio, QueueLength, WorkerExecutionTimeMs,
                    GarbageCollectionCount, ThreadPoolThreads, AsyncOperationsCount, MachineId, Subsystem,
                    Operation, Status, TraceId, CorrelationId, DurationMs
                ) VALUES (
                    $Timestamp, $StartupTimeMs, $AuthenticationTimeMs, $DatabaseLatencyMs, $IpcLatencyMs, $TcpLatencyMs,
                    $DownloadSpeed, $UploadSpeed, $DiskLatencyMs, $CacheHitRatio, $QueueLength, $WorkerExecutionTimeMs,
                    $GarbageCollectionCount, $ThreadPoolThreads, $AsyncOperationsCount, $MachineId, $Subsystem,
                    $Operation, $Status, $TraceId, $CorrelationId, $DurationMs
                );";

            var batchParams = new List<Dictionary<string, object?>>();
            foreach (var snapshot in snapshots)
            {
                batchParams.Add(MapParameters(snapshot));
            }

            if (batchParams.Count > 0)
            {
                await _storageProvider.ExecuteBatchAsync(sql, batchParams, cancellationToken);
            }
        }

        public async Task<IReadOnlyCollection<PerformanceSnapshot>> QueryAsync(
            DateTime? start = null,
            DateTime? end = null,
            string? subsystem = null,
            string? machineId = null,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM PerformanceSnapshots WHERE 1=1";
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
            if (!string.IsNullOrEmpty(subsystem))
            {
                sql += " AND Subsystem = $Subsystem";
                parameters["$Subsystem"] = subsystem;
            }
            if (!string.IsNullOrEmpty(machineId))
            {
                sql += " AND MachineId = $MachineId";
                parameters["$MachineId"] = machineId;
            }
            if (!string.IsNullOrEmpty(correlationId))
            {
                sql += " AND CorrelationId = $CorrelationId";
                parameters["$CorrelationId"] = correlationId;
            }

            sql += " ORDER BY Timestamp DESC;";

            return await _storageProvider.QueryAsync(sql, parameters, ReadRecord, cancellationToken);
        }

        public async Task DeleteAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
        {
            var sql = "DELETE FROM PerformanceSnapshots WHERE Timestamp < $Before;";
            var parameters = new Dictionary<string, object?>
            {
                ["$Before"] = beforeUtc.ToString("O")
            };
            await _storageProvider.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        }

        private static Dictionary<string, object?> MapParameters(PerformanceSnapshot s)
        {
            return new Dictionary<string, object?>
            {
                ["$Timestamp"] = s.Timestamp.ToString("O"),
                ["$StartupTimeMs"] = (long)s.StartupTime.TotalMilliseconds,
                ["$AuthenticationTimeMs"] = (long)s.AuthenticationTime.TotalMilliseconds,
                ["$DatabaseLatencyMs"] = (long)s.DatabaseLatency.TotalMilliseconds,
                ["$IpcLatencyMs"] = (long)s.IpcLatency.TotalMilliseconds,
                ["$TcpLatencyMs"] = (long)s.TcpLatency.TotalMilliseconds,
                ["$DownloadSpeed"] = s.DownloadSpeed,
                ["$UploadSpeed"] = s.UploadSpeed,
                ["$DiskLatencyMs"] = (long)s.DiskLatency.TotalMilliseconds,
                ["$CacheHitRatio"] = s.CacheHitRatio,
                ["$QueueLength"] = s.QueueLength,
                ["$WorkerExecutionTimeMs"] = (long)s.WorkerExecutionTime.TotalMilliseconds,
                ["$GarbageCollectionCount"] = s.GarbageCollectionCount,
                ["$ThreadPoolThreads"] = s.ThreadPoolThreads,
                ["$AsyncOperationsCount"] = s.AsyncOperationsCount,
                ["$MachineId"] = s.MachineId,
                ["$Subsystem"] = s.Subsystem,
                ["$Operation"] = s.Operation,
                ["$Status"] = s.Status,
                ["$TraceId"] = s.TraceId,
                ["$CorrelationId"] = s.CorrelationId,
                ["$DurationMs"] = (long)s.Duration.TotalMilliseconds
            };
        }

        private static PerformanceSnapshot ReadRecord(IDataRecord r)
        {
            return new PerformanceSnapshot
            {
                Timestamp = DateTime.Parse(r.GetString(r.GetOrdinal("Timestamp"))),
                StartupTime = TimeSpan.FromMilliseconds(r.GetInt64(r.GetOrdinal("StartupTimeMs"))),
                AuthenticationTime = TimeSpan.FromMilliseconds(r.GetInt64(r.GetOrdinal("AuthenticationTimeMs"))),
                DatabaseLatency = TimeSpan.FromMilliseconds(r.GetInt64(r.GetOrdinal("DatabaseLatencyMs"))),
                IpcLatency = TimeSpan.FromMilliseconds(r.GetInt64(r.GetOrdinal("IpcLatencyMs"))),
                TcpLatency = TimeSpan.FromMilliseconds(r.GetInt64(r.GetOrdinal("TcpLatencyMs"))),
                DownloadSpeed = r.GetDouble(r.GetOrdinal("DownloadSpeed")),
                UploadSpeed = r.GetDouble(r.GetOrdinal("UploadSpeed")),
                DiskLatency = TimeSpan.FromMilliseconds(r.GetInt64(r.GetOrdinal("DiskLatencyMs"))),
                CacheHitRatio = r.GetDouble(r.GetOrdinal("CacheHitRatio")),
                QueueLength = r.GetInt32(r.GetOrdinal("QueueLength")),
                WorkerExecutionTime = TimeSpan.FromMilliseconds(r.GetInt64(r.GetOrdinal("WorkerExecutionTimeMs"))),
                GarbageCollectionCount = r.GetInt32(r.GetOrdinal("GarbageCollectionCount")),
                ThreadPoolThreads = r.GetInt32(r.GetOrdinal("ThreadPoolThreads")),
                AsyncOperationsCount = r.GetInt32(r.GetOrdinal("AsyncOperationsCount")),
                MachineId = r.GetString(r.GetOrdinal("MachineId")),
                Subsystem = r.IsDBNull(r.GetOrdinal("Subsystem")) ? null : r.GetString(r.GetOrdinal("Subsystem")),
                Operation = r.IsDBNull(r.GetOrdinal("Operation")) ? null : r.GetString(r.GetOrdinal("Operation")),
                Status = r.IsDBNull(r.GetOrdinal("Status")) ? null : r.GetString(r.GetOrdinal("Status")),
                TraceId = r.IsDBNull(r.GetOrdinal("TraceId")) ? null : r.GetString(r.GetOrdinal("TraceId")),
                CorrelationId = r.IsDBNull(r.GetOrdinal("CorrelationId")) ? null : r.GetString(r.GetOrdinal("CorrelationId")),
                Duration = TimeSpan.FromMilliseconds(r.GetInt64(r.GetOrdinal("DurationMs")))
            };
        }
    }
}
