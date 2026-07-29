namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the result state of a security hardening or integrity validation operation.
    /// </summary>
    public enum SecurityValidationState
    {
        /// <summary>
        /// Validation completed successfully. The target has valid signatures and is trusted.
        /// </summary>
        Passed,

        /// <summary>
        /// Validation failed due to missing components or minor configuration issues, but without direct tampering evidence.
        /// </summary>
        Failed,

        /// <summary>
        /// Validation passed with warnings, such as minor configuration changes or non-critical file updates.
        /// </summary>
        Warning,

        /// <summary>
        /// The validated asset lacks a valid digital signature or trusted certification chain.
        /// </summary>
        Untrusted,

        /// <summary>
        /// Active tampering or signature/hash corruption has been detected on core system assets.
        /// </summary>
        Tampered
    }
}
