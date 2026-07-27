using System;

namespace Sayra.Client.Shared.Models
{
    public class FleetAlert
    {
        public string AlertId { get; set; } = string.Empty;
        public string MachineId { get; set; } = string.Empty;
        public string RuleId { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Threshold { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public int CooldownSeconds { get; set; }
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Active";
        public DateTime? ResolvedAt { get; set; }
        public int EscalationLevel { get; set; }
    }
}
