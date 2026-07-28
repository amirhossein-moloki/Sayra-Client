using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the immutable validation result of a certificate pinning check.
    /// </summary>
    public class CertificateValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the certificate pinning check succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the error message if validation failed.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets a value indicating whether the certificate matches pinned thumbprints or public keys.
        /// </summary>
        public bool PinnedValid { get; }

        /// <summary>
        /// Gets a value indicating whether the certificate issuer is trusted.
        /// </summary>
        public bool IssuerValid { get; }

        /// <summary>
        /// Gets a value indicating whether the certificate is currently within its validity period.
        /// </summary>
        public bool NotExpired { get; }

        public CertificateValidationResult(bool success, string? errorMessage = null, bool pinnedValid = false, bool issuerValid = false, bool notExpired = false)
        {
            Success = success;
            ErrorMessage = errorMessage;
            PinnedValid = pinnedValid;
            IssuerValid = issuerValid;
            NotExpired = notExpired;
        }

        public static CertificateValidationResult Successful() => new CertificateValidationResult(true, null, true, true, true);
        public static CertificateValidationResult Failed(string errorMessage, bool pinnedValid = false, bool issuerValid = false, bool notExpired = false) =>
            new CertificateValidationResult(false, errorMessage, pinnedValid, issuerValid, notExpired);
    }
}
