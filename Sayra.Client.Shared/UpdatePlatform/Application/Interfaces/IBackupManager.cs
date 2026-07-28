using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Governs the complete backup lifecycle including creation, validation, restoration, and cleanup.
    /// </summary>
    public interface IBackupManager
    {
        /// <summary>
        /// Creates a validated, signed, or encrypted backup.
        /// </summary>
        Task<BackupSnapshot> CreateBackupAsync(string backupId, string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates backup package structure.
        /// </summary>
        Task<bool> ValidateBackupAsync(BackupSnapshot backup, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores a backup to the target directory.
        /// </summary>
        Task<bool> RestoreBackupAsync(BackupSnapshot backup, string targetDirectory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cleans up expired backups exceeding retention limits.
        /// </summary>
        Task CleanupExpiredBackupsAsync(string destinationDirectory, TimeSpan retentionPeriod, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies the cryptographic integrity/hash of the backup snapshot.
        /// </summary>
        Task<bool> VerifyBackupIntegrityAsync(BackupSnapshot backup, CancellationToken cancellationToken = default);
    }
}
