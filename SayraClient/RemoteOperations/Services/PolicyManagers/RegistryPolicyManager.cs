using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;

namespace SayraClient.RemoteOperations.Services
{
    public class RegistryPolicyManager
    {
        private readonly ILogger<RegistryPolicyManager> _logger;
        private readonly ConcurrentDictionary<string, object> _simulatedRegistry = new();
        private readonly ConcurrentDictionary<string, object> _rollbackSnapshot = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public RegistryPolicyManager(ILogger<RegistryPolicyManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private bool IsTestOrNonWindows()
        {
            return !OperatingSystem.IsWindows() || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAYRA_TEST_DB_PATH"));
        }

        public async Task<bool> ApplyRegistryPolicyAsync(string action, string value, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Applying registry policy action '{Action}' with value '{Value}'", action, value);

                string subKeyPath = "";
                string valueName = "";
                object regValue = 0;
                RegistryValueKind valueKind = RegistryValueKind.DWord;

                switch (action.ToUpperInvariant())
                {
                    case "HIDE_DRIVES":
                        subKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
                        valueName = "NoDrives";
                        regValue = int.TryParse(value, out int drivesVal) ? drivesVal : 0;
                        valueKind = RegistryValueKind.DWord;
                        break;
                    case "DISABLE_CONTROL_PANEL":
                        subKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
                        valueName = "NoControlPanel";
                        regValue = value == "true" || value == "1" ? 1 : 0;
                        valueKind = RegistryValueKind.DWord;
                        break;
                    case "DISABLE_TASK_MANAGER":
                        subKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
                        valueName = "DisableTaskMgr";
                        regValue = value == "true" || value == "1" ? 1 : 0;
                        valueKind = RegistryValueKind.DWord;
                        break;
                    case "DISABLE_REGISTRY_EDITOR":
                        subKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
                        valueName = "DisableRegistryTools";
                        regValue = value == "true" || value == "1" ? 1 : 0;
                        valueKind = RegistryValueKind.DWord;
                        break;
                    case "DISABLE_COMMAND_PROMPT":
                        subKeyPath = @"Software\Policies\Microsoft\Windows\System";
                        valueName = "DisableCMD";
                        regValue = value == "true" || value == "1" ? 1 : 0;
                        valueKind = RegistryValueKind.DWord;
                        break;
                    case "DISABLE_POWERSHELL":
                        subKeyPath = @"Software\Policies\Microsoft\Windows\System";
                        valueName = "DisablePowerShell";
                        regValue = value == "true" || value == "1" ? 1 : 0;
                        valueKind = RegistryValueKind.DWord;
                        break;
                    case "DESKTOP_RESTRICTION":
                        subKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop";
                        valueName = "NoHTMLWallPaper";
                        regValue = value == "true" || value == "1" ? 1 : 0;
                        valueKind = RegistryValueKind.DWord;
                        break;
                    case "EXPLORER_RESTRICTION":
                        subKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
                        valueName = "NoClose";
                        regValue = value == "true" || value == "1" ? 1 : 0;
                        valueKind = RegistryValueKind.DWord;
                        break;
                    default:
                        _logger.LogWarning("Unknown registry policy action: {Action}", action);
                        return false;
                }

                string registryKeyFullPath = $@"HKCU\{subKeyPath}\{valueName}";

                object previousValue = GetRegistryValueInternal(subKeyPath, valueName);
                _rollbackSnapshot.TryAdd(registryKeyFullPath, previousValue ?? DBNull.Value);

                SetRegistryValueInternal(subKeyPath, valueName, regValue, valueKind);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply registry policy: {Action}", action);
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RollbackRegistryPoliciesAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Rolling back modified registry policies from {Count} snapshot entries...", _rollbackSnapshot.Count);

                foreach (var entry in _rollbackSnapshot)
                {
                    string keyFull = entry.Key;
                    object originalVal = entry.Value;

                    if (keyFull.StartsWith(@"HKCU\"))
                    {
                        string subPath = keyFull.Substring(5);
                        int lastSlash = subPath.LastIndexOf('\\');
                        if (lastSlash > 0)
                        {
                            string subKeyPath = subPath.Substring(0, lastSlash);
                            string valueName = subPath.Substring(lastSlash + 1);

                            if (originalVal == DBNull.Value || originalVal == null)
                            {
                                DeleteRegistryValueInternal(subKeyPath, valueName);
                            }
                            else
                            {
                                RegistryValueKind kind = RegistryValueKind.DWord;
                                if (originalVal is string) kind = RegistryValueKind.String;
                                SetRegistryValueInternal(subKeyPath, valueName, originalVal, kind);
                            }
                        }
                    }
                }

                _rollbackSnapshot.Clear();
                _logger.LogInformation("Registry policies rolled back successfully.");
            }
            finally
            {
                _lock.Release();
            }
        }

        private object GetRegistryValueInternal(string subKeyPath, string valueName)
        {
            if (!IsTestOrNonWindows())
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(subKeyPath);
                    return key?.GetValue(valueName);
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                _simulatedRegistry.TryGetValue($@"{subKeyPath}\{valueName}", out var val);
                return val;
            }
        }

        private void SetRegistryValueInternal(string subKeyPath, string valueName, object value, RegistryValueKind kind)
        {
            if (!IsTestOrNonWindows())
            {
                using var key = Registry.CurrentUser.CreateSubKey(subKeyPath, true);
                key?.SetValue(valueName, value, kind);
            }
            else
            {
                _simulatedRegistry[$@"{subKeyPath}\{valueName}"] = value;
            }
        }

        private void DeleteRegistryValueInternal(string subKeyPath, string valueName)
        {
            if (!IsTestOrNonWindows())
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(subKeyPath, true);
                    key?.DeleteValue(valueName, false);
                }
                catch
                {
                }
            }
            else
            {
                _simulatedRegistry.TryRemove($@"{subKeyPath}\{valueName}", out _);
            }
        }

        public object GetCurrentPolicyValueForTest(string subKeyPath, string valueName)
        {
            return GetRegistryValueInternal(subKeyPath, valueName);
        }
    }
}
