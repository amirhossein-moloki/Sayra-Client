using System;

namespace Sayra.Client.Shared.Models
{
    public class AuditEntry
    {
        public string AuditId { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string CommandId { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string PreviousHash { get; set; } = string.Empty;
        public string CurrentHash { get; set; } = string.Empty;
    }
}
