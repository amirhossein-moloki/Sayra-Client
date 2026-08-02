using System;

namespace Sayra.Client.Shared.Models.Telemetry.Policies
{
    /// <summary>
    /// Reusable policy governing alert priority/status escalation.
    /// </summary>
    public class EscalationPolicy
    {
        public bool Enabled { get; set; } = true;
        public int DurationMinutesBeforeEscalation { get; set; } = 5;
        public int FrequencyThreshold { get; set; } = 3;
        public string EscalationPriority { get; set; } = "Critical"; // Warning -> Critical -> Emergency
    }
}
