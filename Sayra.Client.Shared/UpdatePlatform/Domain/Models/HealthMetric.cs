using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents structural component health metrics and storage limits.
    /// </summary>
    public class HealthMetric
    {
        public string ComponentName { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string LastErrorMessage { get; set; } = string.Empty;
        public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
        public string CustomMetricsData { get; set; } = string.Empty;
    }
}
