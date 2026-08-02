using System;

namespace Sayra.Client.Shared.Models.Telemetry.Policies
{
    /// <summary>
    /// Reusable policy specifying alert rule evaluation intervals.
    /// </summary>
    public class EvaluationPolicy
    {
        public bool Enabled { get; set; } = true;
        public int IntervalSeconds { get; set; } = 15;
        public string DefaultPriority { get; set; } = "Warning";
    }
}
