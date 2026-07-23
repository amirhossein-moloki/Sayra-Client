using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models
{
    public class EventLogEntry
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public string CorrelationId { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string TraceId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // SECURITY, AUDIT, OPERATIONAL, PERFORMANCE
        public string Severity { get; set; } = string.Empty; // DEBUG, INFO, WARNING, ERROR, FATAL
        public string MessageTemplate { get; set; } = string.Empty;
        public Dictionary<string, object> PayloadFields { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
