using System;

namespace Sayra.Client.Shared.Models
{
    public class BulkOperationResult
    {
        public string ResultId { get; set; } = string.Empty;
        public string OperationId { get; set; } = string.Empty;
        public string WorkstationId { get; set; } = string.Empty;
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string CompletedAt { get; set; } = string.Empty;
    }
}
