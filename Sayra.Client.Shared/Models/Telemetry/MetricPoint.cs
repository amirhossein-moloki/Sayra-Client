using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents a single data point in a metrics time series.
    /// </summary>
    public record MetricPoint
    {
        /// <summary>
        /// Gets the timestamp when the measurement was taken.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the numerical value of the measurement.
        /// </summary>
        public double Value { get; init; }

        /// <summary>
        /// Gets the metadata tags associated with this specific measurement point.
        /// </summary>
        public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
    }
}
