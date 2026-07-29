using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Configuration model for managing failure escalations when normal self-healing recovery loops are exhausted.
    /// This model is immutable and serializable.
    /// </summary>
    public class EscalationPolicy
    {
        /// <summary>
        /// Gets the sequence of recovery actions to execute sequentially upon repeated failure.
        /// </summary>
        public List<RecoveryActionType> EscalationSequence { get; init; } = new();

        /// <summary>
        /// Gets the threshold of failed recovery attempts that triggers the next step in the escalation sequence.
        /// </summary>
        public int AttemptsBeforeEscalation { get; init; } = 5;

        /// <summary>
        /// Gets a value indicating whether remote administrator alerts should be dispatched.
        /// </summary>
        public bool NotifyAdminOnEscalation { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether a workstation reboot is authorized as a final escalation resort.
        /// </summary>
        public bool RebootAuthorized { get; init; }

        /// <summary>
        /// Gets the timeout period before executing the next level of escalation.
        /// </summary>
        public TimeSpan EscalationTimeout { get; init; } = TimeSpan.FromMinutes(1);
    }
}
