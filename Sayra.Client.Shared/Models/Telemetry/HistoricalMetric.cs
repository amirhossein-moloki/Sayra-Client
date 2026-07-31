using System;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents consolidated and downsampled historical metric records for long-term database storage.
    /// </summary>
    public record HistoricalMetric
    {
        /// <summary>
        /// Gets the consolidated timestamp of the historical window.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the identifying name of the metric.
        /// </summary>
        public string MetricName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the category classification of the metric.
        /// </summary>
        public MetricCategory Category { get; init; }

        /// <summary>
        /// Gets the measurement unit.
        /// </summary>
        public MetricUnit Unit { get; init; }

        /// <summary>
        /// Gets the average consolidated value calculated within the interval.
        /// </summary>
        public double AverageValue { get; init; }

        /// <summary>
        /// Gets the minimum value recorded within the interval.
        /// </summary>
        public double MinValue { get; init; }

        /// <summary>
        /// Gets the maximum value recorded within the interval.
        /// </summary>
        public double MaxValue { get; init; }

        /// <summary>
        /// Gets the total count of individual raw metrics aggregated into this record.
        /// </summary>
        public long Count { get; init; }

        /// <summary>
        /// Gets the consolidated interval rollup type (e.g., hourly, daily, weekly, monthly).
        /// </summary>
        public CollectionInterval Interval { get; init; }
    }
}
