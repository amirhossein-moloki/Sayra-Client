using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing numerical metrics mathematical aggregates.
    /// </summary>
    public class MetricsOptions
    {
        /// <summary>
        /// Gets or sets the aggregation window duration in seconds.
        /// </summary>
        [Range(1, 3600, ErrorMessage = "AggregationWindowSeconds must be between 1 and 3600.")]
        public int AggregationWindowSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets a value indicating whether moving averages should be calculated.
        /// </summary>
        public bool EnableMovingAverages { get; set; } = true;
    }
}
