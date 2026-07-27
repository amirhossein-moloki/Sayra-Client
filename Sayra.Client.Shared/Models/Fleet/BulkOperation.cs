using System;

namespace Sayra.Client.Shared.Models
{
    public class BulkOperation
    {
        public string OperationId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetValue { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public BulkOperationStatus Status { get; set; } = BulkOperationStatus.Pending;
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; } = 3;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
