using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Enforces certificate pinning rules including thumbprint, public key, issuer, and expiration validations.
    /// Implements strict fail-closed security behaviors.
    /// </summary>
    public class CertificatePinningService : ICertificatePinningService
    {
        private readonly ILogger<CertificatePinningService> _logger;

        public CertificatePinningService(ILogger<CertificatePinningService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public CertificateValidationResult ValidateCertificate(
            X509Certificate2 certificate,
            string[] expectedThumbprints,
            string[] expectedPublicKeyHashes,
            string[] expectedIssuers)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            _logger.LogInformation("Enforcing certificate pinning check on: {Subject}", certificate.Subject);

            // Strict Fail-Closed Check: If the trust store is empty, fail-closed!
            bool hasThumbprints = expectedThumbprints != null && expectedThumbprints.Length > 0;
            bool hasPublicKeyPins = expectedPublicKeyHashes != null && expectedPublicKeyHashes.Length > 0;

            if (!hasThumbprints && !hasPublicKeyPins)
            {
                var error = "Security breach: Pinning trust store is completely empty. Fail-closed!";
                _logger.LogCritical(error);
                return CertificateValidationResult.Failed(error, pinnedValid: false, issuerValid: false, notExpired: false);
            }

            // 1. Expiration Detection
            var now = DateTime.UtcNow;
            bool isNotExpired = now >= certificate.NotBefore.ToUniversalTime() && now <= certificate.NotAfter.ToUniversalTime();
            if (!isNotExpired)
            {
                var error = $"Certificate has expired or is not yet valid. NotBefore: {certificate.NotBefore}, NotAfter: {certificate.NotAfter}";
                _logger.LogError(error);
                return CertificateValidationResult.Failed(error, pinnedValid: false, issuerValid: false, notExpired: false);
            }

            // 2. Thumbprint Validation (Case-Insensitive)
            bool thumbprintMatched = false;
            if (hasThumbprints)
            {
                string certThumbprint = CleanString(certificate.Thumbprint);
                thumbprintMatched = expectedThumbprints!
                    .Select(CleanString)
                    .Any(t => string.Equals(t, certThumbprint, StringComparison.OrdinalIgnoreCase));

                if (!thumbprintMatched)
                {
                    var error = $"Certificate thumbprint '{certThumbprint}' does not match any pinned thumbprint.";
                    _logger.LogError(error);
                    return CertificateValidationResult.Failed(error, pinnedValid: false, issuerValid: false, notExpired: true);
                }
            }

            // 3. Public Key Pinning (SHA-256 Base64 hash of public key bytes)
            bool publicKeyMatched = false;
            if (hasPublicKeyPins)
            {
                byte[] publicKeyBytes = certificate.GetPublicKey();
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(publicKeyBytes);
                    string computedHashBase64 = Convert.ToBase64String(hashBytes);

                    publicKeyMatched = expectedPublicKeyHashes!.Any(h => string.Equals(h, computedHashBase64, StringComparison.Ordinal));
                    if (!publicKeyMatched)
                    {
                        var error = $"Certificate public key hash '{computedHashBase64}' does not match any pinned public key hash.";
                        _logger.LogError(error);
                        return CertificateValidationResult.Failed(error, pinnedValid: false, issuerValid: false, notExpired: true);
                    }
                }
            }

            // 4. Trusted Issuer Validation
            bool issuerMatched = false;
            if (expectedIssuers != null && expectedIssuers.Length > 0)
            {
                string certIssuer = certificate.Issuer;
                issuerMatched = expectedIssuers.Any(issuer => certIssuer.Contains(issuer, StringComparison.OrdinalIgnoreCase));
                if (!issuerMatched)
                {
                    var error = $"Certificate issuer '{certIssuer}' is not in the trusted issuers list.";
                    _logger.LogError(error);
                    return CertificateValidationResult.Failed(error, pinnedValid: true, issuerValid: false, notExpired: true);
                }
            }
            else
            {
                issuerMatched = true;
            }

            _logger.LogInformation("Certificate pinning check succeeded for: {Subject}", certificate.Subject);
            return CertificateValidationResult.Successful();
        }

        private static string CleanString(string? val)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            return val.Replace(":", "").Replace(" ", "").Trim().ToLowerInvariant();
        }
    }
}
