using System;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Configuration options for the Enterprise Security Hardening Engine.
    /// </summary>
    public class SecurityHardeningOptions
    {
        /// <summary>
        /// Gets or sets the path to the trusted public key used for signature validations.
        /// </summary>
        public string PublicKeyPath { get; set; } = "server_public.key";

        /// <summary>
        /// Gets or sets a value indicating whether configuration file integrity and signatures are validated.
        /// </summary>
        public bool ValidateConfiguration { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether policies are validated and signed.
        /// </summary>
        public bool ValidatePolicy { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether database structural and page validations are run.
        /// </summary>
        public bool ValidateDatabase { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether downloaded ad media hashes are verified.
        /// </summary>
        public bool ValidateMedia { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether folder plugins are validated.
        /// </summary>
        public bool ValidatePlugins { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether staged SPK package authenticity checks are run.
        /// </summary>
        public bool ValidatePackages { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether executable binary Authenticode validation is run.
        /// </summary>
        public bool ValidateExecutable { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the cryptographically chained audit logs are verified.
        /// </summary>
        public bool AuditChainIntegrityCheck { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether command history cryptographic validation is executed.
        /// </summary>
        public bool CommandHistoryCheck { get; set; } = true;

        /// <summary>
        /// Gets or sets the global security policy parameters.
        /// </summary>
        public SecurityPolicy GlobalPolicy { get; set; } = new();
    }
}
