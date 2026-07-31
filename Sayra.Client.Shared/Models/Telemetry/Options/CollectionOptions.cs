using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing background telemetry collection loop frequencies.
    /// </summary>
    public class CollectionOptions
    {
        /// <summary>
        /// Gets or sets the collection interval for critical process and watchdog metrics in seconds.
        /// </summary>
        [Range(1, 60, ErrorMessage = "CriticalIntervalSeconds must be between 1 and 60.")]
        public int CriticalIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets the collection interval for general performance and latency metrics in seconds.
        /// </summary>
        [Range(1, 300, ErrorMessage = "PerformanceIntervalSeconds must be between 1 and 300.")]
        public int PerformanceIntervalSeconds { get; set; } = 15;

        /// <summary>
        /// Gets or sets the collection interval for hardware utilization metrics in seconds.
        /// </summary>
        [Range(1, 600, ErrorMessage = "HardwareIntervalSeconds must be between 1 and 600.")]
        public int HardwareIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the collection interval for disk storage capacity metrics in seconds.
        /// </summary>
        [Range(1, 3600, ErrorMessage = "StorageIntervalSeconds must be between 1 and 3600.")]
        public int StorageIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the consolidation interval for downsampled historical metrics in seconds.
        /// </summary>
        [Range(1, 86400, ErrorMessage = "HistoricalIntervalSeconds must be between 1 and 86400.")]
        public int HistoricalIntervalSeconds { get; set; } = 300;
    }
}
