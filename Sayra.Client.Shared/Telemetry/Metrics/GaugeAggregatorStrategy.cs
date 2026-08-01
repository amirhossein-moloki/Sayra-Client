using System;
using System.Collections.Generic;
using System.Globalization;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Metrics
{
    /// <summary>
    /// Aggregator strategy for gauges representing instantaneous measurements.
    /// Captures the latest (newest) recorded value, and enriches statistical details.
    /// </summary>
    public class GaugeAggregatorStrategy : IMetricAggregatorStrategy
    {
        /// <inheritdoc />
        public AggregationType Type => AggregationType.Gauge;

        /// <inheritdoc />
        public MetricPoint Aggregate(string metricName, IReadOnlyList<TelemetryRecord> records, DateTime windowStart, DateTime windowEnd)
        {
            if (records == null || records.Count == 0)
            {
                return new MetricPoint { Timestamp = windowEnd, Value = 0.0 };
            }

            var values = new List<double>(records.Count);
            var mergedTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Sort records by timestamp to ensure we get the latest
            var sortedRecords = new List<TelemetryRecord>(records);
            sortedRecords.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            for (int i = 0; i < sortedRecords.Count; i++)
            {
                var record = sortedRecords[i];
                values.Add(record.Value);

                if (record.Tags != null)
                {
                    foreach (var kvp in record.Tags)
                    {
                        mergedTags[kvp.Key] = kvp.Value;
                    }
                }
            }

            double lastValue = sortedRecords[^1].Value;
            double min = MetricsMath.CalculateMin(values);
            double max = MetricsMath.CalculateMax(values);
            double avg = MetricsMath.CalculateAverage(values);
            double stdDev = MetricsMath.CalculateStandardDeviation(values);

            mergedTags["aggregation_type"] = "Gauge";
            mergedTags["min"] = min.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["max"] = max.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["avg"] = avg.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["stddev"] = stdDev.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["sample_count"] = values.Count.ToString(CultureInfo.InvariantCulture);

            return new MetricPoint
            {
                Timestamp = windowEnd,
                Value = lastValue,
                Tags = mergedTags
            };
        }
    }
}
