using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Captures download performance, bandwidth constraints, and mirror selection telemetry.
    /// </summary>
    public class DownloadMetric
    {
        public Guid PackageId { get; set; }
        public long TotalSizeBytes { get; set; }
        public TimeSpan Duration { get; set; }
        public double AverageSpeedBytesPerSecond { get; set; }
        public int TotalChunksCount { get; set; }
        public int ResumedChunksCount { get; set; }
        public string PrimaryMirrorUsed { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
    }
}
