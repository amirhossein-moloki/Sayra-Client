using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents a single recorded attempt by the self-healing system to recover a failed subsystem.
    /// This model is immutable and serializable.
    /// </summary>
    public class RecoveryAttempt
    {
        /// <summary>
        /// Gets the unique identifier for this recovery attempt.
        /// </summary>
        public Guid AttemptId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Gets the name of the subsystem being recovered.
        /// </summary>
        public string SubsystemName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the timestamp when the recovery attempt was initiated.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the type or description of the recovery action executed (e.g., RESTART_SERVICE, RECONNECT_DB).
        /// </summary>
        public string ActionTaken { get; init; } = string.Empty;

        /// <summary>
        /// Gets the sequence number of this attempt (e.g., 1 for first try, 2 for retry).
        /// </summary>
        public int AttemptNumber { get; init; }

        /// <summary>
        /// Gets the current status of the recovery operation.
        /// </summary>
        public RecoveryStatus Status { get; init; } = RecoveryStatus.Pending;

        /// <summary>
        /// Gets any diagnostic message associated with this specific attempt.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets optional exception or stack trace details, if the attempt failed due to an error.
        /// </summary>
        public string? ErrorDetails { get; init; }
    }
}
