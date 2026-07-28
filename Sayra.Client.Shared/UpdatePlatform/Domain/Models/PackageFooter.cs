using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the trailer section of a secure package.
    /// </summary>
    public class PackageFooter
    {
        /// <summary>
        /// Gets or sets the digital signature of the preceding package content.
        /// </summary>
        public string Signature { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cryptographic algorithm used for signing (e.g. "ECDsa-P384").
        /// </summary>
        public string Algorithm { get; set; } = "ECDsa-P384";

        /// <summary>
        /// Gets or sets the UTC timestamp when the signature was generated.
        /// </summary>
        public DateTime SignedAt { get; set; }

        /// <summary>
        /// Gets or sets the public key or certificate thumbprint used to sign the package.
        /// </summary>
        public string KeyFingerprint { get; set; } = string.Empty;
    }
}
