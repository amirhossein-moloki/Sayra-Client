using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Manages application and workstation snapshot creation, verification, and restoration.
    /// Packs and restores binaries, databases, and configuration settings atomically.
    /// </summary>
    public class SnapshotManager : ISnapshotManager
    {
        private readonly ILogger<SnapshotManager> _logger;
        private readonly IBackupManager _backupManager;

        public SnapshotManager(ILogger<SnapshotManager> logger, IBackupManager backupManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
        }

        public async Task<BackupSnapshot> CreateSnapshotAsync(string snapshotId, string sourceBinariesDir, string sourceConfigDir, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating snapshot '{SnapshotId}'...", snapshotId);

            if (!Directory.Exists(sourceBinariesDir))
            {
                throw new SnapshotCreationException($"Source binaries directory '{sourceBinariesDir}' does not exist.");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), $"SAYRA_Snapshot_Staging_{snapshotId}_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string targetBinDir = Path.Combine(tempDir, "Binaries");
                string targetConfigDir = Path.Combine(tempDir, "Configurations");

                Directory.CreateDirectory(targetBinDir);
                Directory.CreateDirectory(targetConfigDir);

                // Copy Binaries (exe, dll)
                await CopyDirectoryAsync(sourceBinariesDir, targetBinDir, cancellationToken);

                // Copy Configurations & Databases (if exists)
                if (Directory.Exists(sourceConfigDir))
                {
                    await CopyDirectoryAsync(sourceConfigDir, targetConfigDir, cancellationToken);
                }

                // Write Version Metadata & Manifest Snapshot
                var metadata = new
                {
                    SnapshotId = snapshotId,
                    CreatedAt = DateTime.UtcNow,
                    SourceBinariesPath = sourceBinariesDir,
                    SourceConfigPath = sourceConfigDir
                };

                string metadataPath = Path.Combine(tempDir, "version_metadata.json");
                string jsonString = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metadataPath, jsonString, cancellationToken);

                // Create backed up zip package via backupManager
                string destDir = Path.Combine(Path.GetTempPath(), "SAYRA_Snapshots");
                var snapshotBackup = await _backupManager.CreateBackupAsync(snapshotId, tempDir, destDir, cancellationToken);

                return snapshotBackup;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create snapshot '{SnapshotId}'.", snapshotId);
                throw new SnapshotCreationException($"Failed to create snapshot '{snapshotId}': {ex.Message}", ex);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        public async Task<bool> ValidateSnapshotAsync(BackupSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Validating snapshot '{SnapshotId}'...", snapshot?.BackupId);
            if (snapshot == null) return false;
            return await _backupManager.ValidateBackupAsync(snapshot, cancellationToken);
        }

        public async Task<bool> RestoreSnapshotAsync(BackupSnapshot snapshot, string targetBinariesDir, string targetConfigDir, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Restoring snapshot '{SnapshotId}' to targets...", snapshot?.BackupId);

            if (snapshot == null)
            {
                throw new RollbackFailedException("Snapshot cannot be null.");
            }

            if (!await ValidateSnapshotAsync(snapshot, cancellationToken))
            {
                throw new RollbackFailedException($"Snapshot '{snapshot.BackupId}' failed integrity or structure validation.");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), $"SAYRA_Snapshot_Restoring_{snapshot.BackupId}_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            string tempBinDir = $"{targetBinariesDir}_temp_{Guid.NewGuid()}";
            string oldBinDir = $"{targetBinariesDir}_old_{Guid.NewGuid()}";

            string tempConfDir = $"{targetConfigDir}_temp_{Guid.NewGuid()}";
            string oldConfDir = $"{targetConfigDir}_old_{Guid.NewGuid()}";

            try
            {
                // Extract zip snapshot contents to tempDir first
                await _backupManager.RestoreBackupAsync(snapshot, tempDir, cancellationToken);

                string sourceBinDir = Path.Combine(tempDir, "Binaries");
                string sourceConfigDir = Path.Combine(tempDir, "Configurations");

                // Atomically stage binaries copy
                if (Directory.Exists(sourceBinDir))
                {
                    if (Directory.Exists(tempBinDir)) Directory.Delete(tempBinDir, true);
                    await CopyDirectoryAsync(sourceBinDir, tempBinDir, cancellationToken);
                }

                // Atomically stage configs copy
                if (Directory.Exists(sourceConfigDir))
                {
                    if (Directory.Exists(tempConfDir)) Directory.Delete(tempConfDir, true);
                    await CopyDirectoryAsync(sourceConfigDir, tempConfDir, cancellationToken);
                }

                // Atomic Swap Binaries
                if (Directory.Exists(tempBinDir))
                {
                    if (Directory.Exists(targetBinariesDir))
                    {
                        Directory.Move(targetBinariesDir, oldBinDir);
                        Directory.Move(tempBinDir, targetBinariesDir);
                        try { Directory.Delete(oldBinDir, true); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to cleanup old binaries directory"); }
                    }
                    else
                    {
                        Directory.Move(tempBinDir, targetBinariesDir);
                    }
                }

                // Atomic Swap Configs
                if (Directory.Exists(tempConfDir))
                {
                    if (Directory.Exists(targetConfigDir))
                    {
                        Directory.Move(targetConfigDir, oldConfDir);
                        Directory.Move(tempConfDir, targetConfigDir);
                        try { Directory.Delete(oldConfDir, true); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to cleanup old configs directory"); }
                    }
                    else
                    {
                        Directory.Move(tempConfDir, targetConfigDir);
                    }
                }

                _logger.LogInformation("Snapshot '{SnapshotId}' successfully restored.", snapshot.BackupId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore snapshot '{SnapshotId}'. Attempting safety rollbacks.", snapshot.BackupId);

                // Recover directories if swap failed mid-way
                if (Directory.Exists(oldBinDir) && !Directory.Exists(targetBinariesDir))
                {
                    try { Directory.Move(oldBinDir, targetBinariesDir); } catch { }
                }
                if (Directory.Exists(oldConfDir) && !Directory.Exists(targetConfigDir))
                {
                    try { Directory.Move(oldConfDir, targetConfigDir); } catch { }
                }

                // Clean up temp directories
                if (Directory.Exists(tempBinDir)) try { Directory.Delete(tempBinDir, true); } catch { }
                if (Directory.Exists(tempConfDir)) try { Directory.Delete(tempConfDir, true); } catch { }

                throw new RollbackFailedException($"Failed to restore snapshot '{snapshot.BackupId}': {ex.Message}", ex);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        private async Task CopyDirectoryAsync(string sourceDir, string targetDir, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string targetFilePath = Path.Combine(targetDir, Path.GetFileName(file));

                // Read & write to support async/cancel perfectly
                using (var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                using (var destStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await sourceStream.CopyToAsync(destStream, cancellationToken);
                }
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                await CopyDirectoryAsync(subDir, targetSubDir, cancellationToken);
            }
        }
    }
}
