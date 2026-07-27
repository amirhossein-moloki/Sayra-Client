using System;

namespace Sayra.Client.Shared.Models
{
    public class BulkOperation
    {
        public string OperationId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Shutdown, Restart, etc.
        public string StartedAt { get; set; } = string.Empty;
        public string CompletedAt { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Executing, Succeeded, Failed, Cancelled
        public int SucceededCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public int CancelledCount { get; set; }
        public int RetryCount { get; set; }
    }
}
