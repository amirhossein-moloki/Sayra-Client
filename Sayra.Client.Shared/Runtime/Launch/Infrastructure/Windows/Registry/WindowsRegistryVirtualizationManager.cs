using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Registry
{
    /// <summary>
    /// Implements Application Registry Isolation.
    /// Note: This is an Application-level Registry Isolation layer, not native Windows Kernel-level Registry Virtualization.
    /// It isolates virtual registry branches under HKCU\Software\SAYRA_Virtual\{SessionId}\{GameId} to support concurrent workstation environments.
    /// </summary>
    public class WindowsRegistryVirtualizationManager : IRegistryVirtualizationManager
    {
        private readonly ILogger<WindowsRegistryVirtualizationManager> _logger;
        private const string VirtualRegistryRootPath = @"Software\SAYRA_Virtual";

        public WindowsRegistryVirtualizationManager(ILogger<WindowsRegistryVirtualizationManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task PrepareRegistryAsync(Guid sessionId, string gameId, Dictionary<string, string> virtualKeys)
        {
            if (virtualKeys == null || virtualKeys.Count == 0)
            {
                _logger.LogInformation("PrepareRegistryAsync: No virtual registry keys specified. Skipping.");
                return;
            }

            _logger.LogInformation("Preparing Application Registry Isolation for Game '{GameId}' (Session: {SessionId}) under HKCU\\{Root}\\{SessionId}\\{GameId}",
                gameId, sessionId, VirtualRegistryRootPath, sessionId, gameId);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("PrepareRegistryAsync: Not on Windows. Skipping native registry operations.");
                return;
            }

            try
            {
                string targetSubKeyPath = $"{VirtualRegistryRootPath}\\{sessionId}\\{gameId}";
                using (RegistryKey rootKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(targetSubKeyPath, true))
                {
                    if (rootKey == null)
                    {
                        throw new InvalidOperationException($"Failed to create or open virtual registry subkey: '{targetSubKeyPath}'");
                    }

                    foreach (var kvp in virtualKeys)
                    {
                        rootKey.SetValue(kvp.Key, kvp.Value);
                        _logger.LogDebug("Virtualized Registry Key: {Name} = {Value}", kvp.Key, kvp.Value);
                    }
                }

                _logger.LogInformation("Successfully isolated {Count} registry key(s) under virtual path for Game '{GameId}' (Session: {SessionId}).",
                    virtualKeys.Count, gameId, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to isolate registry keys for Game '{GameId}' (Session: {SessionId}). Rolling back...", gameId, sessionId);
                await CleanupRegistryAsync(sessionId, gameId, virtualKeys);
                throw;
            }
        }

        public async Task CleanupRegistryAsync(Guid sessionId, string gameId, Dictionary<string, string> virtualKeys)
        {
            _logger.LogInformation("Cleaning up Application Registry Isolation keys for Game '{GameId}' (Session: {SessionId})", gameId, sessionId);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("CleanupRegistryAsync: Not on Windows. Skipping native registry cleanup.");
                return;
            }

            try
            {
                string sessionPath = $"{VirtualRegistryRootPath}\\{sessionId}";
                using (RegistryKey sessionKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(sessionPath, true))
                {
                    if (sessionKey != null)
                    {
                        sessionKey.DeleteSubKeyTree(gameId, throwOnMissingSubKey: false);
                        _logger.LogInformation("Successfully deleted virtual game registry subtree for '{GameId}' under Session: {SessionId}", gameId, sessionId);
                    }
                }

                // If the session path itself is empty, clean it up too
                using (RegistryKey rootKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(VirtualRegistryRootPath, true))
                {
                    if (rootKey != null)
                    {
                        using (RegistryKey sessionKeyCheck = rootKey.OpenSubKey(sessionId.ToString()))
                        {
                            if (sessionKeyCheck != null && sessionKeyCheck.SubKeyCount == 0 && sessionKeyCheck.ValueCount == 0)
                            {
                                rootKey.DeleteSubKey(sessionId.ToString(), throwOnMissingSubKey: false);
                                _logger.LogInformation("Cleaned up empty session root subkey for Session: {SessionId}", sessionId);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up virtualized registry keys for Game '{GameId}' (Session: {SessionId}).", gameId, sessionId);
            }
        }
    }
}
