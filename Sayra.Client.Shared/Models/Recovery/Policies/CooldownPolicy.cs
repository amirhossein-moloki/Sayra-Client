using System;

namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Configuration model for enforcing a cooldown quarantine window when a subsystem is repeatedly failing.
    /// This model is immutable and serializable.
    /// </summary>
    public class CooldownPolicy
    {
        /// <summary>
        /// Gets the duration of the cooldown window after exceeding retry limits.
        /// </summary>
        public TimeSpan CooldownDuration { get; init; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets the evaluation period window (e.g., 5 failures within 1 minute triggers cooldown).
        /// </summary>
        public TimeSpan EvaluationWindow { get; init; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets the threshold of failures within the evaluation window that activates the cooldown.
        /// </summary>
        public int FailureThreshold { get; init; } = 3;

        /// <summary>
        /// Gets a value indicating whether self-healing is suspended completely for the subsystem during the active cooldown.
        /// </summary>
        public bool SuspendHealingDuringCooldown { get; init; } = true;
    }
}
