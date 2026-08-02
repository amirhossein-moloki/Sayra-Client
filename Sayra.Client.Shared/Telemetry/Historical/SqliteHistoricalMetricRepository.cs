using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Historical
{
    /// <summary>
    /// SQLite implementation of historical metrics repository.
    /// </summary>
    public class SqliteHistoricalMetricRepository : IHistoricalMetricRepository
    {
        private readonly IHistoricalStorageProvider _storageProvider;

        public SqliteHistoricalMetricRepository(IHistoricalStorageProvider storageProvider)
        {
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        }

        public async Task InsertAsync(HistoricalMetric metric, CancellationToken cancellationToken = default)
        {
            if (metric == null) throw new ArgumentNullException(nameof(metric));

            var sql = @"
                INSERT OR REPLACE INTO HistoricalMetrics (
                    Timestamp, MetricName, Category, Unit, AverageValue, MinValue, MaxValue, Count, Interval
                ) VALUES (
                    $Timestamp, $MetricName, $Category, $Unit, $AverageValue, $MinValue, $MaxValue, $Count, $Interval
                );";

            var parameters = MapParameters(metric);
            await _storageProvider.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        }

        public async Task BatchInsertAsync(IEnumerable<HistoricalMetric> metrics, CancellationToken cancellationToken = default)
        {
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));

            var sql = @"
                INSERT OR REPLACE INTO HistoricalMetrics (
                    Timestamp, MetricName, Category, Unit, AverageValue, MinValue, MaxValue, Count, Interval
                ) VALUES (
                    $Timestamp, $MetricName, $Category, $Unit, $AverageValue, $MinValue, $MaxValue, $Count, $Interval
                );";

            var batchParams = new List<Dictionary<string, object?>>();
            foreach (var metric in metrics)
            {
                batchParams.Add(MapParameters(metric));
            }

            if (batchParams.Count > 0)
            {
                await _storageProvider.ExecuteBatchAsync(sql, batchParams, cancellationToken);
            }
        }

        public async Task<IReadOnlyCollection<HistoricalMetric>> QueryAsync(
            string? name = null,
            DateTime? start = null,
            DateTime? end = null,
            CollectionInterval? interval = null,
            CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM HistoricalMetrics WHERE 1=1";
            var parameters = new Dictionary<string, object?>();

            if (!string.IsNullOrEmpty(name))
            {
                sql += " AND MetricName = $MetricName";
                parameters["$MetricName"] = name;
            }
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
            if (interval.HasValue)
            {
                sql += " AND Interval = $Interval";
                parameters["$Interval"] = (int)interval.Value;
            }

            sql += " ORDER BY Timestamp DESC;";

            return await _storageProvider.QueryAsync(sql, parameters, ReadRecord, cancellationToken);
        }

        public async Task DeleteAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
        {
            var sql = "DELETE FROM HistoricalMetrics WHERE Timestamp < $Before;";
            var parameters = new Dictionary<string, object?>
            {
                ["$Before"] = beforeUtc.ToString("O")
            };
            await _storageProvider.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        }

        public async Task<IReadOnlyCollection<HistoricalMetric>> GetExpiredAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM HistoricalMetrics WHERE Timestamp < $Before ORDER BY Timestamp ASC;";
            var parameters = new Dictionary<string, object?>
            {
                ["$Before"] = beforeUtc.ToString("O")
            };
            return await _storageProvider.QueryAsync(sql, parameters, ReadRecord, cancellationToken);
        }

        private static Dictionary<string, object?> MapParameters(HistoricalMetric m)
        {
            return new Dictionary<string, object?>
            {
                ["$Timestamp"] = m.Timestamp.ToString("O"),
                ["$MetricName"] = m.MetricName,
                ["$Category"] = (int)m.Category,
                ["$Unit"] = (int)m.Unit,
                ["$AverageValue"] = m.AverageValue,
                ["$MinValue"] = m.MinValue,
                ["$MaxValue"] = m.MaxValue,
                ["$Count"] = m.Count,
                ["$Interval"] = (int)m.Interval
            };
        }

        private static HistoricalMetric ReadRecord(IDataRecord r)
        {
            return new HistoricalMetric
            {
                Timestamp = DateTime.Parse(r.GetString(r.GetOrdinal("Timestamp"))),
                MetricName = r.GetString(r.GetOrdinal("MetricName")),
                Category = (MetricCategory)r.GetInt32(r.GetOrdinal("Category")),
                Unit = (MetricUnit)r.GetInt32(r.GetOrdinal("Unit")),
                AverageValue = r.GetDouble(r.GetOrdinal("AverageValue")),
                MinValue = r.GetDouble(r.GetOrdinal("MinValue")),
                MaxValue = r.GetDouble(r.GetOrdinal("MaxValue")),
                Count = r.GetInt64(r.GetOrdinal("Count")),
                Interval = (CollectionInterval)r.GetInt32(r.GetOrdinal("Interval"))
            };
        }
    }
}
