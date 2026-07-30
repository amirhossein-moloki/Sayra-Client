using System;

namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Reusable policy specifying cryptographic and signature verification security levels.
    /// </summary>
    public class SecurityPolicy
    {
        /// <summary>
        /// Gets or sets a value indicating whether cryptographic signatures are required.
        /// </summary>
        public bool SignatureRequired { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether untrusted publishers/signatures are tolerated under warnings.
        /// </summary>
        public bool AllowUntrusted { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether background/active integrity validation is enabled.
        /// </summary>
        public bool IntegrityVerificationEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the name of the expected digital signature verification algorithm (e.g. RSA, ECDsa).
        /// </summary>
        public string AllowedSignatureAlgorithm { get; set; } = "ECDsa";
    }
}
