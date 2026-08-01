using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Metrics
{
    /// <summary>
    /// Implements enterprise-grade downsampling strategies (Average, Maximum, Minimum, Sum, Last Value)
    /// to consolidate high-frequency metrics over larger time intervals.
    /// </summary>
    public static class MetricDownsampler
    {
        /// <summary>
        /// Downsamples a collection of metric points into a single consolidated metric point using the specified strategy.
        /// </summary>
        /// <param name="points">The collection of points to downsample.</param>
        /// <param name="strategy">The strategy to apply (Average, Maximum, Minimum, Sum, LastValue).</param>
        /// <param name="timestamp">The timestamp to assign to the downsampled point (typically the end of the downsampling window).</param>
        /// <returns>A consolidated MetricPoint.</returns>
        public static MetricPoint Downsample(IReadOnlyList<MetricPoint> points, string strategy, DateTime timestamp)
        {
            if (points == null || points.Count == 0)
            {
                return new MetricPoint { Timestamp = timestamp, Value = 0.0 };
            }

            double resultValue = 0.0;
            var mergedTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Harvest values and merge tags
            var values = new List<double>(points.Count);
            foreach (var point in points)
            {
                values.Add(point.Value);
                if (point.Tags != null)
                {
                    foreach (var kvp in point.Tags)
                    {
                        mergedTags[kvp.Key] = kvp.Value;
                    }
                }
            }

            string cleanStrategy = strategy?.Trim().ToLowerInvariant() ?? "average";

            switch (cleanStrategy)
            {
                case "maximum":
                case "max":
                    resultValue = MetricsMath.CalculateMax(values);
                    break;

                case "minimum":
                case "min":
                    resultValue = MetricsMath.CalculateMin(values);
                    break;

                case "sum":
                    resultValue = MetricsMath.CalculateSum(values);
                    break;

                case "lastvalue":
                case "last_value":
                case "last":
                    // Sort by timestamp to find the latest value
                    var sortedPoints = new List<MetricPoint>(points);
                    sortedPoints.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                    resultValue = sortedPoints[^1].Value;
                    break;

                case "average":
                case "avg":
                default:
                    resultValue = MetricsMath.CalculateAverage(values);
                    break;
            }

            mergedTags["downsample_strategy"] = strategy ?? "Average";
            mergedTags["downsample_original_count"] = points.Count.ToString();

            return new MetricPoint
            {
                Timestamp = timestamp,
                Value = resultValue,
                Tags = mergedTags
            };
        }
    }
}
