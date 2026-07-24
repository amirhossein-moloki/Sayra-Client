using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.LocalAdmin.Models;
using Sayra.Client.Configuration.Rollback;

namespace Sayra.Client.Configuration.Storage;

public class ConfigurationApplyService
{
    private readonly ConfigurationRollbackManager _rollbackManager;
    private readonly ILogger<ConfigurationApplyService>? _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigurationApplyService(ConfigurationRollbackManager rollbackManager, ILogger<ConfigurationApplyService>? logger = null)
    {
        _rollbackManager = rollbackManager;
        _logger = logger;
    }

    public async Task<bool> ApplyAtomicAsync(string activePath, string backupPath, string tempPath, ClientConfiguration newConfig, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation($"Acquiring file lock to atomically apply new configuration to '{activePath}'...");
        await _fileLock.WaitAsync(cancellationToken);

        try
        {
            // 1. Ensure directory exists
            string? dir = Path.GetDirectoryName(activePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Clean up old temporary transition file if it exists
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            // 2. Write to the temporary transition file asynchronously
            _logger?.LogInformation($"Writing new configuration to temporary transition file '{tempPath}'...");
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, newConfig, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            // 3. Verify the temporary file is NOT corrupted before overwriting active configuration
            if (_rollbackManager.IsCorrupted(tempPath))
            {
                _logger?.LogError($"Corruption detected in freshly written temporary transition file '{tempPath}'. Aborting application.");
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                return false;
            }

            // 4. Back up active configuration before replacement
            if (File.Exists(activePath))
            {
                _logger?.LogInformation($"Backing up current active configuration to '{backupPath}'...");
                File.Copy(activePath, backupPath, overwrite: true);
            }

            // 5. Atomic Replace
            _logger?.LogInformation($"Swapping temporary transition file to active configuration file '{activePath}'...");
            try
            {
                if (File.Exists(activePath))
                {
                    File.Delete(activePath);
                }
                File.Move(tempPath, activePath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"File.Move failed. Attempting fallback copy/delete for active configuration replacement.");
                File.Copy(tempPath, activePath, overwrite: true);
                File.Delete(tempPath);
            }

            _logger?.LogInformation("Configuration applied atomically and successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Critical error occurred during atomic configuration apply.");
            // Try to cleanup temp file if it exists
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch { /* Ignore cleanup errors */ }
            return false;
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
