using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Metrics
{
    /// <summary>
    /// Strategy interface for a pluggable, type-specific metric aggregator.
    /// </summary>
    public interface IMetricAggregatorStrategy
    {
        /// <summary>
        /// Gets the aggregation type supported by this strategy.
        /// </summary>
        AggregationType Type { get; }

        /// <summary>
        /// Aggregates a set of raw telemetry records in a specific window into a single representative metric point.
        /// </summary>
        /// <param name="metricName">The name of the metric being aggregated.</param>
        /// <param name="records">The raw data points to aggregate.</param>
        /// <param name="windowStart">The beginning of the aggregation window.</param>
        /// <param name="windowEnd">The end of the aggregation window.</param>
        /// <returns>An aggregated MetricPoint.</returns>
        MetricPoint Aggregate(string metricName, IReadOnlyList<TelemetryRecord> records, DateTime windowStart, DateTime windowEnd);
    }
}
