using System;

namespace Sayra.Client.Shared.Models
{
    public class BulkOperationResult
    {
        public string ResultId { get; set; } = string.Empty;
        public string OperationId { get; set; } = string.Empty;
        public string MachineId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public BulkOperationStatus Status { get; set; } = BulkOperationStatus.Pending;
        public DateTime? CompletedAt { get; set; }
    }
}
