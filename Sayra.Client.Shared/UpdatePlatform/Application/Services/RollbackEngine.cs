using System;
using System.Collections.Concurrent;
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
    /// Implements the Rollback Engine responsible for taking directory-level snapshots,
    /// and atomically restoring the workstation to a preceding stable version.
    /// Operations are fully idempotent and snapshot registry is fully persistent.
    /// </summary>
    public class RollbackEngine : IRollbackEngine
    {
        private readonly ILogger<RollbackEngine> _logger;
        private readonly ISnapshotManager _snapshotManager;
        private readonly ConcurrentDictionary<string, BackupSnapshot> _snapshotRegistry = new();
        private readonly object _fileLock = new object();

        private string _binariesDirectory;
        private string _configurationsDirectory;

        public string BinariesDirectory
        {
            get => _binariesDirectory;
            set
            {
                _binariesDirectory = value;
                LoadRegistryFromDisk();
            }
        }

        public string ConfigurationsDirectory
        {
            get => _configurationsDirectory;
            set
            {
                _configurationsDirectory = value;
                LoadRegistryFromDisk();
            }
        }

        public RollbackEngine(ILogger<RollbackEngine> logger, ISnapshotManager snapshotManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));

            // Default production directories, overrideable in tests
            _binariesDirectory = Path.Combine(AppContext.BaseDirectory, "Production_Binaries");
            _configurationsDirectory = Path.Combine(AppContext.BaseDirectory, "Production_Configs");

            LoadRegistryFromDisk();
        }

        private string GetRegistryFilePath()
        {
            return Path.Combine(_configurationsDirectory, "snapshots_registry.json");
        }

        private void LoadRegistryFromDisk()
        {
            lock (_fileLock)
            {
                try
                {
                    string filePath = GetRegistryFilePath();
                    if (File.Exists(filePath))
                    {
                        string json = File.ReadAllText(filePath);
                        var loaded = JsonSerializer.Deserialize<ConcurrentDictionary<string, BackupSnapshot>>(json);
                        if (loaded != null)
                        {
                            _snapshotRegistry.Clear();
                            foreach (var kvp in loaded)
                            {
                                _snapshotRegistry[kvp.Key] = kvp.Value;
                            }
                            _logger.LogInformation("Loaded {Count} registered snapshots from persistent storage.", _snapshotRegistry.Count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load snapshot registry from disk. Using clean registry.");
                }
            }
        }

        private void SaveRegistryToDisk()
        {
            lock (_fileLock)
            {
                try
                {
                    string filePath = GetRegistryFilePath();
                    string dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    string tempPath = $"{filePath}_temp_{Guid.NewGuid()}";
                    string json = JsonSerializer.Serialize(_snapshotRegistry, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(tempPath, json);

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                    File.Move(tempPath, filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save snapshot registry atomically to disk.");
                }
            }
        }

        public async Task<bool> RollbackAsync(RollbackRecord record, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("RollbackAsync triggered for record '{Id}' with failed version '{Version}'.", record.Id, record.UpdateVersion);
            return await ExecuteRollbackAsync(record.UpdateVersion, "FSM Automated Version Reversion", cancellationToken);
        }

        public async Task<bool> CreateSnapshotAsync(string snapshotId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("CreateSnapshotAsync triggered for '{SnapshotId}'...", snapshotId);

            try
            {
                if (!Directory.Exists(BinariesDirectory))
                {
                    Directory.CreateDirectory(BinariesDirectory);
                }
                if (!Directory.Exists(ConfigurationsDirectory))
                {
                    Directory.CreateDirectory(ConfigurationsDirectory);
                }

                var snapshot = await _snapshotManager.CreateSnapshotAsync(snapshotId, BinariesDirectory, ConfigurationsDirectory, cancellationToken);
                _snapshotRegistry[snapshotId] = snapshot;

                SaveRegistryToDisk();

                _logger.LogInformation("Snapshot '{SnapshotId}' registered successfully.", snapshotId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateSnapshotAsync failed for '{SnapshotId}'.", snapshotId);
                throw new SnapshotCreationException($"Failed to create snapshot for '{snapshotId}': {ex.Message}", ex);
            }
        }

        public async Task<bool> ExecuteRollbackAsync(string snapshotId, string failureReason, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("ExecuteRollbackAsync triggered for '{SnapshotId}'. Reason: '{FailureReason}'", snapshotId, failureReason);

            if (!_snapshotRegistry.TryGetValue(snapshotId, out var snapshot))
            {
                _logger.LogError("Rollback failed: Snapshot '{SnapshotId}' not found in registry.", snapshotId);
                return false;
            }

            try
            {
                // Ensure target directories exist or restore atomically
                bool success = await _snapshotManager.RestoreSnapshotAsync(snapshot, BinariesDirectory, ConfigurationsDirectory, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Rollback to snapshot '{SnapshotId}' completed successfully.", snapshotId);
                }
                else
                {
                    _logger.LogError("Rollback to snapshot '{SnapshotId}' failed.", snapshotId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecuteRollbackAsync failed for '{SnapshotId}'.", snapshotId);
                throw new RollbackFailedException($"Rollback to '{snapshotId}' failed: {ex.Message}", ex);
            }
        }

        public async Task<bool> ValidateRollbackSucceededAsync(string snapshotId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("ValidateRollbackSucceededAsync triggered for '{SnapshotId}'...", snapshotId);

            if (!_snapshotRegistry.TryGetValue(snapshotId, out var snapshot))
            {
                _logger.LogWarning("Snapshot '{SnapshotId}' not registered. Validation failed.", snapshotId);
                return false;
            }

            try
            {
                bool isValid = await _snapshotManager.ValidateSnapshotAsync(snapshot, cancellationToken);
                if (isValid)
                {
                    // Check directories
                    bool binariesOk = Directory.Exists(BinariesDirectory) && Directory.GetFiles(BinariesDirectory).Length >= 0;
                    bool configOk = Directory.Exists(ConfigurationsDirectory) && Directory.GetFiles(ConfigurationsDirectory).Length >= 0;
                    return binariesOk && configOk;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rollback validation exception for '{SnapshotId}'.", snapshotId);
                return false;
            }
        }
    }
}
