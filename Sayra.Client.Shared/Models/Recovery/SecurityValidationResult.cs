using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the result of a security validation check performed by the security hardening engine.
    /// This model is immutable and serializable.
    /// </summary>
    public class SecurityValidationResult
    {
        /// <summary>
        /// Gets the unique identifier for this security check.
        /// </summary>
        public Guid CheckId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Gets the name of the validated system asset or file (e.g., config, database, database_reindex, plugin).
        /// </summary>
        public string TargetName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the validation state (Passed, Failed, Tampered, Untrusted).
        /// </summary>
        public SecurityValidationState ValidationState { get; init; } = SecurityValidationState.Passed;

        /// <summary>
        /// Gets the timestamp when the validation check was executed.
        /// </summary>
        public DateTime CheckedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the expected hash or cryptographic signature for the asset.
        /// </summary>
        public string? ExpectedSignature { get; init; }

        /// <summary>
        /// Gets the actual/computed hash or cryptographic signature during validation.
        /// </summary>
        public string? ComputedSignature { get; init; }

        /// <summary>
        /// Gets diagnostic message or description of the security check result.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether this validation represents a critical security anomaly or direct tampering event.
        /// </summary>
        public bool IsTamperDetected => ValidationState == SecurityValidationState.Tampered;
    }
}
