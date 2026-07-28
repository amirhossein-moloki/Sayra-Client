using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Captures installation-specific performance and execution metrics.
    /// </summary>
    public class InstallationMetric
    {
        public string TargetVersion { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public int FilesReplacedCount { get; set; }
        public bool Success { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public long BytesWritten { get; set; }
    }
}
