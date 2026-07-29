using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for verifying cryptographic integrity, signature verification, database validation, and tamper detection.
    /// </summary>
    public interface ISecurityHardeningService
    {
        /// <summary>
        /// Validates system-wide cryptographic integrity including databases, policies, and configuration signatures.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning true if system integrity is verified, false if any anomaly or tampering is detected.</returns>
        Task<bool> VerifySystemIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies SQLCipher local database integrity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning true if the database is sound, false if corrupted or locked.</returns>
        Task<bool> VerifyDatabaseIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies the cryptographic chaining audit log integrity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning true if the audit chain signature is valid, false if tampered or broken.</returns>
        Task<bool> VerifyAuditIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies local applied policy signatures using the trusted public key.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning true if all policies are untampered, false otherwise.</returns>
        Task<bool> VerifyPolicyIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies core local configuration file integrity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning true if config files exist and are valid, false otherwise.</returns>
        Task<bool> VerifyConfigurationIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Computes and verifies checksum hashes for downloaded media and assets.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning true if media assets are valid and match expected checksums, false if tampered.</returns>
        Task<bool> VerifyDownloadedMediaIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies signature integrity on all stored remote command history records.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning true if history records are signed and untampered, false otherwise.</returns>
        Task<bool> VerifyCommandHistoryIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates application configuration (JSON, sensitive settings, environments).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task containing the configuration validation result.</returns>
        Task<SecurityValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates policy files, policy signatures, versions, schema, and integrity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task containing the policy validation result.</returns>
        Task<SecurityValidationResult> ValidatePolicyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates SQLite database integrity, schema version, and index consistency.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task containing the database validation result.</returns>
        Task<SecurityValidationResult> ValidateDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates media hashes, metadata, duplicate hashes, and corruption in downloaded media files.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task containing the media validation result.</returns>
        Task<SecurityValidationResult> ValidateMediaAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates plugins (signatures, manifests, metadata, versions, compatibility, and missing dependencies).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task containing the plugin validation result.</returns>
        Task<SecurityValidationResult> ValidatePluginsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates downloaded packages (hashes, signatures, manifests, versions, and trusted publisher).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task containing the package validation result.</returns>
        Task<SecurityValidationResult> ValidatePackagesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates executable binary hashes, integrity, trusted signatures, version information, and metadata.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task containing the executable validation result.</returns>
        Task<SecurityValidationResult> ValidateExecutableAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a full concurrent system security validation, executing all specific validation checks.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning a list of all validation results.</returns>
        Task<IReadOnlyList<SecurityValidationResult>> RunFullValidationAsync(CancellationToken cancellationToken = default);
    }
}
