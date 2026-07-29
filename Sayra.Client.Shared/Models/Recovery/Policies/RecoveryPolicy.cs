using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Represents the full, unified recovery policy configuration for a managed subsystem.
    /// Combines retry, cooldown, escalation, dependency, and maximum retry limit configurations.
    /// This model is immutable and serializable.
    /// </summary>
    public class RecoveryPolicy
    {
        /// <summary>
        /// Gets the name of the managed subsystem that this policy governs.
        /// </summary>
        public string SubsystemName { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether this recovery policy is actively enabled.
        /// </summary>
        public bool IsEnabled { get; init; } = true;

        /// <summary>
        /// Gets the priority tier of this recovery policy relative to other subsystem recoveries.
        /// </summary>
        public RecoveryPriority Priority { get; init; } = RecoveryPriority.Normal;

        /// <summary>
        /// Gets the default recovery action executed on initial failure.
        /// </summary>
        public RecoveryActionType DefaultAction { get; init; } = RecoveryActionType.RestartWorker;

        /// <summary>
        /// Gets the retry policy configuration.
        /// </summary>
        public RetryPolicy Retry { get; init; } = new();

        /// <summary>
        /// Gets the cooldown policy configuration to prevent restart loops.
        /// </summary>
        public CooldownPolicy Cooldown { get; init; } = new();

        /// <summary>
        /// Gets the escalation policy configuration for handling unrecovered failures.
        /// </summary>
        public EscalationPolicy Escalation { get; init; } = new();

        /// <summary>
        /// Gets the dependency policy configuration.
        /// </summary>
        public DependencyPolicy Dependency { get; init; } = new();

        /// <summary>
        /// Gets the maximum retry limits and threshold configuration.
        /// </summary>
        public MaximumRetryConfiguration LimitConfig { get; init; } = new();
    }
}
