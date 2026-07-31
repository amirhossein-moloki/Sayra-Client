using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing workstation alert generation thresholds.
    /// </summary>
    public class AlertOptions
    {
        /// <summary>
        /// Gets or sets the warning CPU utilization percentage threshold (0.0 to 100.0).
        /// </summary>
        [Range(1.0, 100.0, ErrorMessage = "CpuThresholdPercent must be between 1.0 and 100.0.")]
        public double CpuThresholdPercent { get; set; } = 90.0;

        /// <summary>
        /// Gets or sets the warning RAM utilization percentage threshold (0.0 to 100.0).
        /// </summary>
        [Range(1.0, 100.0, ErrorMessage = "MemoryThresholdPercent must be between 1.0 and 100.0.")]
        public double MemoryThresholdPercent { get; set; } = 90.0;

        /// <summary>
        /// Gets or sets the minimum allowed disk free space threshold percentage (0.0 to 100.0).
        /// </summary>
        [Range(1.0, 100.0, ErrorMessage = "DiskFreeSpaceThresholdPercent must be between 1.0 and 100.0.")]
        public double DiskFreeSpaceThresholdPercent { get; set; } = 10.0;

        /// <summary>
        /// Gets or sets the threshold alert suppression cooldown period in seconds.
        /// </summary>
        [Range(1, 3600, ErrorMessage = "CooldownPeriodSeconds must be between 1 and 3600.")]
        public int CooldownPeriodSeconds { get; set; } = 300;
    }
}
