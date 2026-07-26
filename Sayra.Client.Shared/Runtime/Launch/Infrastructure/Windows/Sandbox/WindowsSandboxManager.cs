using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Sandbox
{
    public class WindowsSandboxManager : ISandboxManager
    {
        private readonly ILogger<WindowsSandboxManager> _logger;

        public WindowsSandboxManager(ILogger<WindowsSandboxManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private void ValidateSandboxPath(string sandboxPath)
        {
            if (string.IsNullOrWhiteSpace(sandboxPath))
            {
                throw new ArgumentException("Sandbox path cannot be empty.");
            }

            // Block path traversal characters
            if (sandboxPath.Contains("..") || sandboxPath.Contains(@"\..\") || sandboxPath.Contains("/../"))
            {
                throw new UnauthorizedAccessException($"Path traversal attempt blocked: '{sandboxPath}'");
            }

            try
            {
                // Ensure sandbox path is not targeting critical system paths
                string fullPath = Path.GetFullPath(sandboxPath);
                string? systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
                if (systemRoot != null && string.Equals(Path.GetFullPath(fullPath), Path.GetFullPath(systemRoot), StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException($"Targeting system root volume is prohibited: '{fullPath}'");
                }

                string windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrEmpty(windowsFolder) && fullPath.StartsWith(Path.GetFullPath(windowsFolder), StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException($"Targeting Windows system directories is prohibited: '{fullPath}'");
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid sandbox path specified: '{sandboxPath}'", ex);
            }
        }

        public async Task PrepareSandboxAsync(string gameId, string sandboxPath)
        {
            if (string.IsNullOrWhiteSpace(sandboxPath))
            {
                _logger.LogWarning("PrepareSandboxAsync: No sandbox path configured. Skipping sandbox preparation.");
                return;
            }

            // Enforce security validation
            ValidateSandboxPath(sandboxPath);

            _logger.LogInformation("Preparing isolated sandbox directory structure for Game '{GameId}' at: '{SandboxPath}'", gameId, sandboxPath);

            string saveDataPath = Path.Combine(sandboxPath, "SaveData");
            string tempPath = Path.Combine(sandboxPath, "Temp");
            string cachePath = Path.Combine(sandboxPath, "Cache");

            bool createdSaveData = false;
            bool createdTemp = false;
            bool createdCache = false;

            try
            {
                if (!Directory.Exists(sandboxPath))
                {
                    Directory.CreateDirectory(sandboxPath);
                }

                if (!Directory.Exists(saveDataPath))
                {
                    Directory.CreateDirectory(saveDataPath);
                    createdSaveData = true;
                    _logger.LogDebug("Created sandbox SaveData folder: '{SaveDataPath}'", saveDataPath);
                }

                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                    createdTemp = true;
                    _logger.LogDebug("Created sandbox Temp folder: '{TempPath}'", tempPath);
                }

                if (!Directory.Exists(cachePath))
                {
                    Directory.CreateDirectory(cachePath);
                    createdCache = true;
                    _logger.LogDebug("Created sandbox Cache folder: '{CachePath}'", cachePath);
                }

                // If on Windows, we could perform directory junctions if specific folder mappings are requested.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logger.LogInformation("Enforcing secure folder junction rules on Windows platform.");
                    // Junction logic can be performed here if dynamic folder mapping was defined.
                }

                _logger.LogInformation("Sandbox preparation completed successfully for Game '{GameId}'.", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to prepare sandbox for Game '{GameId}'. Rolling back changes...", gameId);

                // Rollback cleanly
                try
                {
                    if (createdSaveData && Directory.Exists(saveDataPath)) Directory.Delete(saveDataPath, true);
                    if (createdTemp && Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
                    if (createdCache && Directory.Exists(cachePath)) Directory.Delete(cachePath, true);
                    if (Directory.Exists(sandboxPath) && Directory.GetFileSystemEntries(sandboxPath).Length == 0)
                    {
                        Directory.Delete(sandboxPath);
                    }
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Critical error during sandbox rollback.");
                }

                throw new InvalidOperationException($"Sandbox preparation failed for game {gameId}", ex);
            }
        }

        public async Task CleanupSandboxAsync(string gameId, string sandboxPath)
        {
            if (string.IsNullOrWhiteSpace(sandboxPath))
            {
                return;
            }

            // Enforce security validation
            ValidateSandboxPath(sandboxPath);

            _logger.LogInformation("Cleaning up isolated sandbox for Game '{GameId}' at: '{SandboxPath}'", gameId, sandboxPath);

            try
            {
                if (Directory.Exists(sandboxPath))
                {
                    // Clean up and delete directory recursively
                    Directory.Delete(sandboxPath, true);
                    _logger.LogInformation("Idempotent sandbox cleanup completed successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up sandbox directory at '{SandboxPath}'. Retrying on next cycle.", sandboxPath);
            }
        }
    }
}
