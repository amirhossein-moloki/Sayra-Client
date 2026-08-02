using System;

namespace Sayra.Client.Shared.Models.Telemetry.Policies
{
    /// <summary>
    /// Reusable policy for automatic alert recovery/resolution.
    /// </summary>
    public class RecoveryPolicy
    {
        public bool AutoResolve { get; set; } = true;
        public double RecoveryThreshold { get; set; }
        public string RecoveryOperator { get; set; } = "LessThan";
        public int ConsecutiveNormalSamplesRequired { get; set; } = 3;
    }
}
