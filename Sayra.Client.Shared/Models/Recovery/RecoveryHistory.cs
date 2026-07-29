using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the full historical log of self-healing operations, failures, and recovery results for a specific subsystem.
    /// This model is serializable.
    /// </summary>
    public class RecoveryHistory
    {
        /// <summary>
        /// Gets or sets the name of the managed subsystem.
        /// </summary>
        public string SubsystemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of failures ever recorded for this subsystem.
        /// </summary>
        public int TotalFailures { get; set; }

        /// <summary>
        /// Gets or sets the total number of successful recovery/self-healing events.
        /// </summary>
        public int TotalSuccessfulRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the list of all recorded failure events.
        /// </summary>
        public List<FailureRecord> Failures { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of all self-healing recovery attempts and their results.
        /// </summary>
        public List<RecoveryResult> RecoveryResults { get; set; } = new();

        /// <summary>
        /// Gets or sets custom properties or metrics tracking stability patterns for this subsystem.
        /// </summary>
        public Dictionary<string, string> DiagnosticsMetadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
