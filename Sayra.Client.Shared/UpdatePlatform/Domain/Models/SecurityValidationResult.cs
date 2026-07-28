using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the immutable final result of a binary or package security validation pipeline.
    /// </summary>
    public class SecurityValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the security validation was successful.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the error message if the security validation failed.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets the extracted publisher name from the signature.
        /// </summary>
        public string? Publisher { get; }

        /// <summary>
        /// Gets the thumbprint of the signing certificate.
        /// </summary>
        public string? Thumbprint { get; }

        /// <summary>
        /// Gets a value indicating whether the certificate is expired.
        /// </summary>
        public bool IsExpired { get; }

        /// <summary>
        /// Gets a value indicating whether the full trust chain is valid.
        /// </summary>
        public bool IsChainValid { get; }

        public SecurityValidationResult(bool success, string? errorMessage = null, string? publisher = null, string? thumbprint = null, bool isExpired = false, bool isChainValid = false)
        {
            Success = success;
            ErrorMessage = errorMessage;
            Publisher = publisher;
            Thumbprint = thumbprint;
            IsExpired = isExpired;
            IsChainValid = isChainValid;
        }

        public static SecurityValidationResult Successful(string? publisher = null, string? thumbprint = null) =>
            new SecurityValidationResult(true, null, publisher, thumbprint, false, true);

        public static SecurityValidationResult Failed(string errorMessage, string? publisher = null, string? thumbprint = null, bool isExpired = false, bool isChainValid = false) =>
            new SecurityValidationResult(false, errorMessage, publisher, thumbprint, isExpired, isChainValid);
    }
}
