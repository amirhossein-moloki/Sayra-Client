using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Persists system rollback operations and logs.
    /// </summary>
    public class RollbackHistoryRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Reason { get; set; } = string.Empty;
        public string TriggerSource { get; set; } = string.Empty;
        public string PreviousVersion { get; set; } = string.Empty;
        public string RestoredVersion { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string Result { get; set; } = string.Empty;
        public string FailureDetails { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
