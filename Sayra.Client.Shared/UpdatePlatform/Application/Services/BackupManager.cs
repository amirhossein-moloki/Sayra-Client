using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Production-ready system backup management implementation.
    /// Supports compression, validation, restoration, and retention of system states.
    /// </summary>
    public class BackupManager : IBackupManager
    {
        private readonly ILogger<BackupManager> _logger;

        public BackupManager(ILogger<BackupManager> logger)
        {
            _logger = logger;
        }

        public async Task<BackupSnapshot> CreateBackupAsync(string backupId, string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating backup '{BackupId}' of directory '{SourceDirectory}'...", backupId, sourceDirectory);

            if (!Directory.Exists(sourceDirectory))
            {
                throw new BackupValidationException($"Source directory '{sourceDirectory}' does not exist.");
            }

            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            string targetZipPath = Path.Combine(destinationDirectory, $"{backupId}.zip");

            try
            {
                if (File.Exists(targetZipPath))
                {
                    File.Delete(targetZipPath);
                }

                // Compress synchronously on worker thread or tasks
                await Task.Run(() => ZipFile.CreateFromDirectory(sourceDirectory, targetZipPath), cancellationToken);

                var fileInfo = new FileInfo(targetZipPath);
                string hash = await ComputeSha256Async(targetZipPath, cancellationToken);

                var backup = new BackupSnapshot
                {
                    BackupId = backupId,
                    FilePath = targetZipPath,
                    CreatedAt = DateTime.UtcNow,
                    Sha256Hash = hash,
                    SizeBytes = fileInfo.Length,
                    IsValid = true
                };

                _logger.LogInformation("Backup '{BackupId}' successfully created. Size: {Size} bytes. Hash: {Hash}", backupId, backup.SizeBytes, hash);
                return backup;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create backup '{BackupId}'.", backupId);
                throw new SnapshotCreationException($"Failed to create backup '{backupId}': {ex.Message}", ex);
            }
        }

        public async Task<bool> ValidateBackupAsync(BackupSnapshot backup, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Validating backup '{BackupId}' at '{FilePath}'...", backup?.BackupId, backup?.FilePath);

            if (backup == null || string.IsNullOrEmpty(backup.FilePath) || !File.Exists(backup.FilePath))
            {
                _logger.LogWarning("Backup validation failed: file not found.");
                return false;
            }

            try
            {
                // Verify hash matches
                string currentHash = await ComputeSha256Async(backup.FilePath, cancellationToken);
                if (!string.Equals(currentHash, backup.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Backup '{BackupId}' hash mismatch. Expected: {Expected}, Computed: {Computed}", backup.BackupId, backup.Sha256Hash, currentHash);
                    return false;
                }

                // Check ZIP integrity
                bool zipValid = await Task.Run(() =>
                {
                    try
                    {
                        using (var archive = ZipFile.OpenRead(backup.FilePath))
                        {
                            return archive.Entries != null;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }, cancellationToken);

                return zipValid;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Backup validation encountered an exception for '{BackupId}'.", backup.BackupId);
                return false;
            }
        }

        public async Task<bool> RestoreBackupAsync(BackupSnapshot backup, string targetDirectory, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Restoring backup '{BackupId}' to target '{TargetDirectory}'...", backup?.BackupId, targetDirectory);

            if (backup == null || !await ValidateBackupAsync(backup, cancellationToken))
            {
                throw new BackupValidationException($"Backup '{backup?.BackupId}' is corrupt or invalid.");
            }

            string tempTargetDir = $"{targetDirectory}_temp_{Guid.NewGuid()}";
            string oldTargetDir = $"{targetDirectory}_old_{Guid.NewGuid()}";

            try
            {
                if (Directory.Exists(tempTargetDir))
                {
                    Directory.Delete(tempTargetDir, true);
                }
                Directory.CreateDirectory(tempTargetDir);

                // Extract to temporary staging folder first (atomic staging)
                await Task.Run(() => ZipFile.ExtractToDirectory(backup.FilePath, tempTargetDir), cancellationToken);

                // Atomic directory swap
                if (Directory.Exists(targetDirectory))
                {
                    Directory.Move(targetDirectory, oldTargetDir);
                    Directory.Move(tempTargetDir, targetDirectory);
                    try
                    {
                        Directory.Delete(oldTargetDir, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old temporary backup directory '{OldDir}'", oldTargetDir);
                    }
                }
                else
                {
                    string parent = Path.GetDirectoryName(targetDirectory);
                    if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }
                    Directory.Move(tempTargetDir, targetDirectory);
                }

                _logger.LogInformation("Backup '{BackupId}' restored successfully via atomic swap to '{TargetDirectory}'.", backup.BackupId, targetDirectory);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore backup '{BackupId}'. Attempting safety rollback of folders.", backup.BackupId);

                // Rollback swap if failed mid-way
                if (Directory.Exists(oldTargetDir) && !Directory.Exists(targetDirectory))
                {
                    try
                    {
                        Directory.Move(oldTargetDir, targetDirectory);
                    }
                    catch (Exception rbEx)
                    {
                        _logger.LogCritical(rbEx, "Failed to rollback folder swap for target '{Target}'", targetDirectory);
                    }
                }

                // Cleanup temp folder
                if (Directory.Exists(tempTargetDir))
                {
                    try { Directory.Delete(tempTargetDir, true); } catch { }
                }

                throw new RollbackFailedException($"Failed to restore backup '{backup.BackupId}': {ex.Message}", ex);
            }
        }

        public async Task CleanupExpiredBackupsAsync(string destinationDirectory, TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Cleaning up expired backups in '{DestinationDirectory}' with retention: {Retention}...", destinationDirectory, retentionPeriod);

            if (!Directory.Exists(destinationDirectory))
            {
                return;
            }

            await Task.Run(() =>
            {
                var files = Directory.GetFiles(destinationDirectory, "*.zip");
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (DateTime.UtcNow - fileInfo.CreationTimeUtc > retentionPeriod)
                        {
                            _logger.LogInformation("Deleting expired backup file: '{File}'", file);
                            fileInfo.Delete();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete expired backup file '{File}'.", file);
                    }
                }
            }, cancellationToken);
        }

        public async Task<bool> VerifyBackupIntegrityAsync(BackupSnapshot backup, CancellationToken cancellationToken = default)
        {
            return await ValidateBackupAsync(backup, cancellationToken);
        }

        private async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                var sb = new StringBuilder();
                foreach (byte b in sha256.Hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
