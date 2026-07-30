using System;

namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Reusable policy for defining metric warning, critical, and emergency threshold limits.
    /// </summary>
    public class ThresholdPolicy
    {
        /// <summary>
        /// Gets or sets the warning threshold value.
        /// </summary>
        public double Warning { get; set; }

        /// <summary>
        /// Gets or sets the critical threshold value.
        /// </summary>
        public double Critical { get; set; }

        /// <summary>
        /// Gets or sets the emergency threshold value.
        /// </summary>
        public double Emergency { get; set; }
    }
}
