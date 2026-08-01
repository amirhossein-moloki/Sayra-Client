using System;
using System.Collections.Generic;
using System.Globalization;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Metrics
{
    /// <summary>
    /// Aggregator strategy for timers tracking latency and operation execution durations.
    /// Captures the average duration, and enriches percentile metadata (P50, P90, P95, P99), sum, and count.
    /// </summary>
    public class TimerAggregatorStrategy : IMetricAggregatorStrategy
    {
        /// <inheritdoc />
        public AggregationType Type => AggregationType.Timer;

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

                if (record.Tags != null)
                {
                    foreach (var kvp in record.Tags)
                    {
                        mergedTags[kvp.Key] = kvp.Value;
                    }
                }
            }

            double avg = MetricsMath.CalculateAverage(values);
            double min = MetricsMath.CalculateMin(values);
            double max = MetricsMath.CalculateMax(values);
            double sum = MetricsMath.CalculateSum(values);
            double stdDev = MetricsMath.CalculateStandardDeviation(values);
            double p50 = MetricsMath.CalculatePercentile(values, 50);
            double p90 = MetricsMath.CalculatePercentile(values, 90);
            double p95 = MetricsMath.CalculatePercentile(values, 95);
            double p99 = MetricsMath.CalculatePercentile(values, 99);

            mergedTags["aggregation_type"] = "Timer";
            mergedTags["min_ms"] = min.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["max_ms"] = max.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["sum_ms"] = sum.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["avg_ms"] = avg.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["stddev_ms"] = stdDev.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["p50_ms"] = p50.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["p90_ms"] = p90.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["p95_ms"] = p95.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["p99_ms"] = p99.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["sample_count"] = values.Count.ToString(CultureInfo.InvariantCulture);

            return new MetricPoint
            {
                Timestamp = windowEnd,
                Value = avg, // Primary value is average duration
                Tags = mergedTags
            };
        }
    }
}
