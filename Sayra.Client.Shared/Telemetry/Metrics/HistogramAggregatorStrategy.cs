using System;
using System.Collections.Generic;
using System.Globalization;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Metrics
{
    /// <summary>
    /// Aggregator strategy for histograms mapping frequency distribution.
    /// Calculates high-precision statistical percentiles (P50, P90, P95, P99), min, max, stddev, and variance.
    /// </summary>
    public class HistogramAggregatorStrategy : IMetricAggregatorStrategy
    {
        /// <inheritdoc />
        public AggregationType Type => AggregationType.Histogram;

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
            double stdDev = MetricsMath.CalculateStandardDeviation(values);
            double variance = MetricsMath.CalculateVariance(values);
            double p50 = MetricsMath.CalculatePercentile(values, 50);
            double p90 = MetricsMath.CalculatePercentile(values, 90);
            double p95 = MetricsMath.CalculatePercentile(values, 95);
            double p99 = MetricsMath.CalculatePercentile(values, 99);

            mergedTags["aggregation_type"] = "Histogram";
            mergedTags["min"] = min.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["max"] = max.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["avg"] = avg.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["stddev"] = stdDev.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["variance"] = variance.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["p50"] = p50.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["p90"] = p90.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["p95"] = p95.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["p99"] = p99.ToString("F2", CultureInfo.InvariantCulture);
            mergedTags["sample_count"] = values.Count.ToString(CultureInfo.InvariantCulture);

            return new MetricPoint
            {
                Timestamp = windowEnd,
                Value = avg, // Primary value is average
                Tags = mergedTags
            };
        }
    }
}
