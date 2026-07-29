using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents a detailed, structured log entry of a recorded subsystem failure or critical anomaly.
    /// This model is immutable and serializable.
    /// </summary>
    public class FailureRecord
    {
        /// <summary>
        /// Gets the unique identifier for this failure record.
        /// </summary>
        public Guid RecordId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Gets the name of the subsystem that failed.
        /// </summary>
        public string SubsystemName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the severity of the failure (Info, Warning, Error, Critical, Fatal).
        /// </summary>
        public FailureSeverity Severity { get; init; } = FailureSeverity.Error;

        /// <summary>
        /// Gets the timestamp when the failure occurred or was detected.
        /// </summary>
        public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the description or error message reported.
        /// </summary>
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary>
        /// Gets the full exception details or stack trace associated with the failure, if any.
        /// </summary>
        public string? ExceptionTrace { get; init; }

        /// <summary>
        /// Gets a unique Correlation ID to trace this failure across diagnostic logs and recovery attempts.
        /// </summary>
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }
}
