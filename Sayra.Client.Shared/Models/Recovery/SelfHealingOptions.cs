using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Configuration options for the Enterprise Self-Healing Engine.
    /// </summary>
    public class SelfHealingOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether autonomous self-healing is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the absolute limit of consecutive failures for raw recovery attempts.
        /// </summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>
        /// Gets or sets the duration after which consecutive attempt counters are reset.
        /// </summary>
        public TimeSpan AttemptsResetDuration { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Gets or sets the list of recovery policies mapped by subsystem name.
        /// </summary>
        public List<RecoveryPolicy> SubsystemPolicies { get; set; } = new();
    }
}
