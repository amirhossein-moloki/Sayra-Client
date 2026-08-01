using System;
using System.Collections.Generic;
using System.Globalization;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Metrics
{
    /// <summary>
    /// Aggregator strategy for Rates measuring frequency of events or values per second.
    /// Calculates event count frequency and accumulated value throughput rates.
    /// </summary>
    public class RateAggregatorStrategy : IMetricAggregatorStrategy
    {
        /// <inheritdoc />
        public AggregationType Type => AggregationType.Rate;

        /// <inheritdoc />
        public MetricPoint Aggregate(string metricName, IReadOnlyList<TelemetryRecord> records, DateTime windowStart, DateTime windowEnd)
        {
            double durationSeconds = (windowEnd - windowStart).TotalSeconds;
            if (durationSeconds <= 0.0)
            {
                // Fallback to a minimum window of 1 second if timestamps are equal
                durationSeconds = 1.0;
            }

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

            double sum = MetricsMath.CalculateSum(values);
            double count = values.Count;

            // Events per second rate
            double eventsPerSecond = count / durationSeconds;
            // Throughput (value sum) per second rate
            double throughputPerSecond = sum / durationSeconds;

            // Determine primary rate value:
            // If the metric name indicates throughput, we represent throughput per second,
            // otherwise we represent events per second.
            string lowerName = metricName.ToLowerInvariant();
            bool isThroughputMetric = lowerName.Contains("bytes") || lowerName.Contains("speed") ||
                                      lowerName.Contains("download") || lowerName.Contains("upload");

            double primaryRate = isThroughputMetric ? throughputPerSecond : eventsPerSecond;

            mergedTags["aggregation_type"] = "Rate";
            mergedTags["duration_seconds"] = durationSeconds.ToString("F3", CultureInfo.InvariantCulture);
            mergedTags["events_per_second"] = eventsPerSecond.ToString("F4", CultureInfo.InvariantCulture);
            mergedTags["throughput_per_second"] = throughputPerSecond.ToString("F4", CultureInfo.InvariantCulture);
            mergedTags["sum"] = sum.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["count"] = count.ToString(CultureInfo.InvariantCulture);

            return new MetricPoint
            {
                Timestamp = windowEnd,
                Value = primaryRate,
                Tags = mergedTags
            };
        }
    }
}
