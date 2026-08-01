using System;
using System.Collections.Generic;
using System.Globalization;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Metrics
{
    /// <summary>
    /// Aggregator strategy for incremental and cumulative counters.
    /// Sums the values of all raw telemetry records in the window.
    /// </summary>
    public class CounterAggregatorStrategy : IMetricAggregatorStrategy
    {
        /// <inheritdoc />
        public AggregationType Type => AggregationType.Counter;

        /// <inheritdoc />
        public MetricPoint Aggregate(string metricName, IReadOnlyList<TelemetryRecord> records, DateTime windowStart, DateTime windowEnd)
        {
            if (records == null || records.Count == 0)
            {
                return new MetricPoint { Timestamp = windowEnd, Value = 0.0 };
            }

            var values = new List<double>(records.Count);
            var mergedTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                values.Add(record.Value);

                // Merge tags from records
                if (record.Tags != null)
                {
                    foreach (var kvp in record.Tags)
                    {
                        mergedTags[kvp.Key] = kvp.Value;
                    }
                }
            }

            double sum = MetricsMath.CalculateSum(values);
            double count = values.Count;

            mergedTags["aggregation_type"] = "Counter";
            mergedTags["raw_count"] = count.ToString(CultureInfo.InvariantCulture);
            mergedTags["sum"] = sum.ToString(CultureInfo.InvariantCulture);

            return new MetricPoint
            {
                Timestamp = windowEnd,
                Value = sum,
                Tags = mergedTags
            };
        }
    }
}
