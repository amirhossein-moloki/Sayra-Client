using System.Collections.Generic;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents a time-ordered collection of metric data points for a specific metric.
    /// </summary>
    public record MetricSeries
    {
        /// <summary>
        /// Gets the identifying name of the metric series.
        /// </summary>
        public string MetricName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the logical category of the metric.
        /// </summary>
        public MetricCategory Category { get; init; }

        /// <summary>
        /// Gets the unit of measurement.
        /// </summary>
        public MetricUnit Unit { get; init; }

        /// <summary>
        /// Gets the read-only collection of metric points in the series.
        /// </summary>
        public IReadOnlyCollection<MetricPoint> Points { get; init; } = new List<MetricPoint>();
    }
}
