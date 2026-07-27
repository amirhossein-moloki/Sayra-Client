using System;

namespace Sayra.Client.Shared.Models
{
    public class AlertRule
    {
        public string RuleId { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string Threshold { get; set; } = string.Empty;
        public string Severity { get; set; } = "Warning";
        public int CooldownSeconds { get; set; } = 60;
        public int EscalationTimeoutSeconds { get; set; } = 300;
        public bool AutoResolve { get; set; } = true;
        public string EscalationPath { get; set; } = string.Empty;
    }
}
