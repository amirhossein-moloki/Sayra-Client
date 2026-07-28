using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Tracks recovery and rollback metrics including restored file counts and failure roots.
    /// </summary>
    public class RollbackMetric
    {
        public string FailedVersion { get; set; } = string.Empty;
        public string RestoredVersion { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public int FilesRestoredCount { get; set; }
        public bool Success { get; set; }
    }
}
