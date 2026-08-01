using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing numerical metrics mathematical aggregates.
    /// </summary>
    public class MetricsOptions
    {
        /// <summary>
        /// Gets or sets the default aggregation window duration in seconds.
        /// </summary>
        [Range(1, 3600, ErrorMessage = "AggregationWindowSeconds must be between 1 and 3600.")]
        public int AggregationWindowSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets a value indicating whether moving averages should be calculated.
        /// </summary>
        public bool EnableMovingAverages { get; set; } = true;

        /// <summary>
        /// Gets or sets the list of configured aggregation window durations in seconds.
        /// Supporting: 5 seconds, 15 seconds, 30 seconds, 1 minute, 5 minutes, 15 minutes, 1 hour.
        /// </summary>
        public List<int> ConfiguredWindowsSeconds { get; set; } = new() { 5, 15, 30, 60, 300, 900, 3600 };

        /// <summary>
        /// Gets or sets the active downsampling strategy (Average, Maximum, Minimum, Sum, LastValue).
        /// </summary>
        public string DownsamplingStrategy { get; set; } = "Average";
    }
}
