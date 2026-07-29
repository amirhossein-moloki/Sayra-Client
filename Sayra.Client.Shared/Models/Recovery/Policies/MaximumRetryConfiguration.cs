using System;

namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Configuration model for defining maximum retry boundaries and threshold limits to prevent infinite recovery loops.
    /// This model is immutable and serializable.
    /// </summary>
    public class MaximumRetryConfiguration
    {
        /// <summary>
        /// Gets the absolute maximum number of retries allowed over the lifetime of a subsystem instance before lock-down.
        /// </summary>
        public int AbsoluteMaxRetries { get; init; } = 5;

        /// <summary>
        /// Gets the maximum retry attempts permitted within a specific rolling temporal window.
        /// </summary>
        public int MaxRetriesInWindow { get; init; } = 3;

        /// <summary>
        /// Gets the temporal window duration for tracking retries (e.g., maximum 3 retries in any 15 minute window).
        /// </summary>
        public TimeSpan RollingWindowDuration { get; init; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets a value indicating whether exceeding this maximum retry threshold disables automated self-healing for the subsystem.
        /// </summary>
        public bool DisableHealingOnThresholdExceeded { get; init; } = true;
    }
}
