using System;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents a forecasted capacity trend model calculated from long-term historical metrics.
    /// </summary>
    public record CapacityForecast
    {
        /// <summary>
        /// Gets the identifying name of the forecasted metric.
        /// </summary>
        public string MetricName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the current baseline usage level.
        /// </summary>
        public double CurrentUsage { get; init; }

        /// <summary>
        /// Gets the forecasted usage projection level.
        /// </summary>
        public double ForecastedUsage { get; init; }

        /// <summary>
        /// Gets the target future timestamp (horizon) of this projection.
        /// </summary>
        public DateTime ForecastHorizon { get; init; } = DateTime.UtcNow.AddDays(30);

        /// <summary>
        /// Gets the statistical confidence index of the forecast (0.0 to 1.0).
        /// </summary>
        public double ConfidenceLevel { get; init; }

        /// <summary>
        /// Gets the recommended administrative action (e.g., 'Upgrade Disk Space', 'No Action Required').
        /// </summary>
        public string Recommendation { get; init; } = string.Empty;
    }
}
