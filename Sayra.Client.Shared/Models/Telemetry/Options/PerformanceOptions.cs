using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing performance monitoring warning metrics.
    /// </summary>
    public class PerformanceOptions
    {
        /// <summary>
        /// Gets or sets the latency threshold in milliseconds above which warning traces are flagged.
        /// </summary>
        [Range(10, 10000, ErrorMessage = "LatencyWarningThresholdMilliseconds must be between 10 and 10000.")]
        public int LatencyWarningThresholdMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum RAM resource limit before generating optimization warnings.
        /// </summary>
        [Range(10, 2048, ErrorMessage = "MemoryLimitMegabytes must be between 10 and 2048.")]
        public int MemoryLimitMegabytes { get; set; } = 512;
    }
}
