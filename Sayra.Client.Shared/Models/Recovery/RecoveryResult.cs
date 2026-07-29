using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the final outcome of an executed recovery policy or action on a failed subsystem.
    /// This model is immutable and serializable.
    /// </summary>
    public class RecoveryResult
    {
        /// <summary>
        /// Gets the unique identifier matching the corresponding recovery attempt.
        /// </summary>
        public Guid AttemptId { get; init; }

        /// <summary>
        /// Gets the name of the subsystem that underwent recovery.
        /// </summary>
        public string SubsystemName { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the recovery action successfully restored the subsystem's health.
        /// </summary>
        public bool IsSuccessful { get; init; }

        /// <summary>
        /// Gets the operational status representing the recovery result state.
        /// </summary>
        public RecoveryStatus FinalStatus { get; init; }

        /// <summary>
        /// Gets the timestamp when the recovery operation finalized.
        /// </summary>
        public DateTime CompletedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the total duration of the recovery operation.
        /// </summary>
        public TimeSpan Duration { get; init; }

        /// <summary>
        /// Gets the final message detailing the recovery result.
        /// </summary>
        public string OutputMessage { get; init; } = string.Empty;

        /// <summary>
        /// Gets any error details if the recovery was unsuccessful.
        /// </summary>
        public string? ErrorDetails { get; init; }
    }
}
