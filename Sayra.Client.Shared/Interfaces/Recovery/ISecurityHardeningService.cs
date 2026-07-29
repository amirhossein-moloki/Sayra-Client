using System.Threading;
using System.Threading.Tasks;

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
    }
}
