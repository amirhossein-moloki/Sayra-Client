using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.Configuration.Rollback;

public class ConfigurationRollbackManager
{
    private readonly ILogger<ConfigurationRollbackManager>? _logger;

    public ConfigurationRollbackManager(ILogger<ConfigurationRollbackManager>? logger = null)
    {
        _logger = logger;
    }

    public bool IsCorrupted(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger?.LogWarning($"Configuration file '{filePath}' does not exist (considered corrupted/missing).");
            return true;
        }

        try
        {
            string content = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger?.LogWarning($"Configuration file '{filePath}' is empty.");
                return true;
            }

            // Try to parse it to check JSON integrity
            using (var doc = JsonDocument.Parse(content))
            {
                // Verify it has essential properties to be considered valid
                var root = doc.RootElement;
                if (!root.TryGetProperty("ServerDiscovery", out _) && !root.TryGetProperty("GameLibrary", out _))
                {
                    _logger?.LogWarning($"Configuration file '{filePath}' is missing essential properties.");
                    return true;
                }
            }

            return false;
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, $"Configuration file '{filePath}' failed JSON parsing.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to inspect configuration file '{filePath}' due to unexpected error.");
            return true;
        }
    }

    public bool BackupBeforeApply(string activePath, string backupPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            if (!File.Exists(activePath))
            {
                _logger?.LogInformation($"No active configuration file at '{activePath}' to back up. Skipping backup.");
                return true;
            }

            string? dir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(activePath, backupPath, overwrite: true);
            _logger?.LogInformation($"Successfully backed up active configuration file to '{backupPath}'.");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to create backup: {ex.Message}";
            _logger?.LogError(ex, errorMessage);
            return false;
        }
    }

    public bool Rollback(string activePath, string backupPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            if (!File.Exists(backupPath))
            {
                errorMessage = $"Backup file '{backupPath}' does not exist. Cannot perform rollback.";
                _logger?.LogError(errorMessage);
                return false;
            }

            string? dir = Path.GetDirectoryName(activePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(backupPath, activePath, overwrite: true);
            _logger?.LogWarning($"Successfully rolled back configuration: restored backup from '{backupPath}' to '{activePath}'.");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Error occurred during rollback copy: {ex.Message}";
            _logger?.LogError(ex, errorMessage);
            return false;
        }
    }

    public bool ValidateAndRecover(string activePath, string backupPath, out string outcomeMessage)
    {
        outcomeMessage = string.Empty;

        // Corruption check on active configuration
        if (IsCorrupted(activePath))
        {
            _logger?.LogWarning("Active configuration file is corrupted or missing. Initiating startup recovery...");

            if (File.Exists(backupPath) && !IsCorrupted(backupPath))
            {
                bool rolledBack = Rollback(activePath, backupPath, out var rollbackError);
                if (rolledBack)
                {
                    outcomeMessage = "Active configuration was corrupted. Successfully recovered from backup file.";
                    _logger?.LogInformation(outcomeMessage);
                    return true;
                }
                else
                {
                    outcomeMessage = $"Active configuration was corrupted, and recovery from backup failed: {rollbackError}";
                    _logger?.LogError(outcomeMessage);
                }
            }
            else
            {
                outcomeMessage = "Active configuration was corrupted, and backup file is also corrupted or missing.";
                _logger?.LogError(outcomeMessage);
            }

            // Fallback: Create clean default configuration so that the application can boot in safe lockdown mode
            try
            {
                string? dir = Path.GetDirectoryName(activePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var defaultConfig = new ClientConfiguration
                {
                    ClientId = Guid.NewGuid().ToString(),
                    StationId = "STATION_DEFAULT",
                    StationName = "Default Station"
                };

                string defaultJson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(activePath, defaultJson);

                outcomeMessage += " Restored a clean default configuration as ultimate fallback.";
                _logger?.LogWarning("Restored clean default configuration as ultimate fallback.");
                return true;
            }
            catch (Exception ex)
            {
                outcomeMessage += $" Ultimate fallback failed: {ex.Message}";
                _logger?.LogCritical(ex, "Failed to write clean default configuration fallback.");
                return false;
            }
        }

        outcomeMessage = "Active configuration file is healthy.";
        return true;
    }
}
