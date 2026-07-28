using System;
using System.Security.Cryptography.X509Certificates;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Enforces certificate pinning rules including thumbprint, public key, issuer, and expiration validations.
    /// </summary>
    public interface ICertificatePinningService
    {
        /// <summary>
        /// Validates a certificate against configured pins and policies.
        /// </summary>
        /// <param name="certificate">The certificate to validate.</param>
        /// <param name="expectedThumbprints">The collection of allowed SHA-256/SHA-1 certificate thumbprints.</param>
        /// <param name="expectedPublicKeyHashes">The collection of allowed Base64 public key hashes.</param>
        /// <param name="expectedIssuers">The collection of allowed certificate issuers.</param>
        /// <returns>A certificate validation result.</returns>
        CertificateValidationResult ValidateCertificate(X509Certificate2 certificate, string[] expectedThumbprints, string[] expectedPublicKeyHashes, string[] expectedIssuers);
    }
}
