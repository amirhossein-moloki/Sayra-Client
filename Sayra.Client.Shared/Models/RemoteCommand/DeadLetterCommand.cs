using System;

namespace Sayra.Client.Shared.Models
{
    public class DeadLetterCommand
    {
        public string CommandId { get; set; } = string.Empty;
        public string OriginalAction { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string MovedAt { get; set; } = string.Empty;
    }
}
