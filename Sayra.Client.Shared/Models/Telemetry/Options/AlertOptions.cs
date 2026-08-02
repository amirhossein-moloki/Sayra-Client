using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sayra.Client.Shared.Models.Telemetry.Policies;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing workstation alert generation thresholds.
    /// </summary>
    public class AlertOptions
    {
        /// <summary>
        /// Gets or sets rule configurations mapped by rule name.
        /// </summary>
        public Dictionary<string, AlertPolicyConfig> Rules { get; set; } = new();

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

    /// <summary>
    /// Bundles all six reusable policy settings for an individual alert rule.
    /// </summary>
    public class AlertPolicyConfig
    {
        public ThresholdPolicy Threshold { get; set; } = new();
        public SuppressionPolicy Suppression { get; set; } = new();
        public EscalationPolicy Escalation { get; set; } = new();
        public RecoveryPolicy Recovery { get; set; } = new();
        public RateLimitPolicy RateLimit { get; set; } = new();
        public EvaluationPolicy Evaluation { get; set; } = new();
    }
}
