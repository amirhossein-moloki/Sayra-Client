using System;

namespace Sayra.Client.Shared.Models
{
    public class RemoteCommandHistory
    {
        public string CommandId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string TargetPcId { get; set; } = string.Empty;
        public string SenderAdminId { get; set; } = string.Empty;
        public string? PayloadJson { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public string ReceivedAt { get; set; } = string.Empty;
        public string? StartedAt { get; set; }
        public string? CompletedAt { get; set; }
        public long? ExecutionDurationMs { get; set; }
        public string Signature { get; set; } = string.Empty;
        public int RetryCount { get; set; }
    }
}
