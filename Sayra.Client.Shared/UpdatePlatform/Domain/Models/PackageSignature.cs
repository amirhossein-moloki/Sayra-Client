using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the parsed digital signature of a package or manifest.
    /// </summary>
    public class PackageSignature
    {
        /// <summary>
        /// Gets or sets the raw cryptographic signature bytes.
        /// </summary>
        public byte[] RawSignatureBytes { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the signature algorithm identifier.
        /// </summary>
        public string Algorithm { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the certificate or key identification fingerprint.
        /// </summary>
        public string KeyFingerprint { get; set; } = string.Empty;
    }
}
