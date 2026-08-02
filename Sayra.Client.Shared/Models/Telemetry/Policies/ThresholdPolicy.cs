using System;

namespace Sayra.Client.Shared.Models.Telemetry.Policies
{
    /// <summary>
    /// Reusable policy for defining threshold operators and warning, critical, and emergency values.
    /// </summary>
    public class ThresholdPolicy
    {
        public string Operator { get; set; } = "GreaterThan"; // GreaterThan, LessThan, Equal, NotEqual, Range, Percentage, Boolean
        public double? Value { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public bool? BooleanValue { get; set; }
        public double? WarningValue { get; set; }
        public double? CriticalValue { get; set; }
        public double? EmergencyValue { get; set; }
    }
}
