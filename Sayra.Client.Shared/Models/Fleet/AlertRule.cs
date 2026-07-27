using System;

namespace Sayra.Client.Shared.Models
{
    public class AlertRule
    {
        public string RuleId { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string ThresholdExpression { get; set; } = string.Empty; // e.g. "> 90", "== Offline"
        public int CooldownMinutes { get; set; }
        public string Severity { get; set; } = "Warning"; // Critical, Warning, Information
        public bool AutoResolve { get; set; }
        public bool EscalationEnabled { get; set; }
        public int EscalationThresholdMinutes { get; set; }
    }
}
