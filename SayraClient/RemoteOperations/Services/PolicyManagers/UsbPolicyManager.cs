using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;

namespace SayraClient.RemoteOperations.Services
{
    public class UsbPolicyManager
    {
        private readonly ILogger<UsbPolicyManager> _logger;
        private readonly ConcurrentDictionary<string, object> _simulatedStorage = new();
        private readonly ConcurrentBag<string> _approvedDevices = new();
        private readonly ConcurrentBag<string> _whitelistedDevices = new();
        private readonly ConcurrentBag<string> _blacklistedDevices = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private int? _originalUsbStorStartValue;

        public bool SimulateNonAdminForTest { get; set; } = false;

        public UsbPolicyManager(ILogger<UsbPolicyManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private bool IsTestOrNonWindows()
        {
            return !OperatingSystem.IsWindows() || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAYRA_TEST_DB_PATH"));
        }

        private bool IsAdministrator()
        {
            if (SimulateNonAdminForTest) return false;

            if (!IsTestOrNonWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            return true;
        }

        public async Task<bool> ApplyUsbPolicyAsync(string action, string value, List<string> devices = null, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Applying USB policy action '{Action}' with value '{Value}'", action, value);

                if (!IsAdministrator())
                {
                    _logger.LogWarning("Insufficient privileges to apply sensitive USB policy modifications.");
                    throw new SecurityException("Sensitive registry modifications require elevated administrator privileges.");
                }

                switch (action.ToUpperInvariant())
                {
                    case "USB_BLOCK":
                        bool shouldBlock = value == "true" || value == "1";
                        await SetUsbStorStartValueAsync(shouldBlock ? 4 : 3);
                        break;

                    case "USB_ALLOW_APPROVED":
                        _approvedDevices.Clear();
                        if (devices != null)
                        {
                            foreach (var d in devices) _approvedDevices.Add(d);
                        }
                        _logger.LogInformation("Configured {Count} approved USB devices.", _approvedDevices.Count);
                        break;

                    case "USB_WHITELIST":
                        _whitelistedDevices.Clear();
                        if (devices != null)
                        {
                            foreach (var d in devices) _whitelistedDevices.Add(d);
                        }
                        _logger.LogInformation("Configured {Count} whitelisted USB devices.", _whitelistedDevices.Count);
                        break;

                    case "USB_BLACKLIST":
                        _blacklistedDevices.Clear();
                        if (devices != null)
                        {
                            foreach (var d in devices) _blacklistedDevices.Add(d);
                        }
                        _logger.LogInformation("Configured {Count} blacklisted USB devices.", _blacklistedDevices.Count);
                        break;

                    default:
                        _logger.LogWarning("Unknown USB policy action: {Action}", action);
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply USB policy action: {Action}", action);
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RollbackUsbPoliciesAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Rolling back USB and Device policies...");

                if (_originalUsbStorStartValue.HasValue)
                {
                    await SetUsbStorStartValueAsync(_originalUsbStorStartValue.Value, true);
                    _originalUsbStorStartValue = null;
                }

                _approvedDevices.Clear();
                _whitelistedDevices.Clear();
                _blacklistedDevices.Clear();

                _logger.LogInformation("USB and Device policies rolled back successfully.");
            }
            finally
            {
                _lock.Release();
            }
        }

        private Task SetUsbStorStartValueAsync(int value, bool isRollback = false)
        {
            if (!IsTestOrNonWindows())
            {
                const string keyPath = @"SYSTEM\CurrentControlSet\Services\USBSTOR";
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                    if (key != null)
                    {
                        if (!isRollback && !_originalUsbStorStartValue.HasValue)
                        {
                            var original = key.GetValue("Start");
                            if (original is int origVal)
                            {
                                _originalUsbStorStartValue = origVal;
                            }
                        }
                        key.SetValue("Start", value, RegistryValueKind.DWord);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to modify USBSTOR Start registry key.");
                    throw;
                }
            }
            else
            {
                if (!isRollback && !_originalUsbStorStartValue.HasValue)
                {
                    _simulatedStorage.TryGetValue("USBSTOR_Start", out var orig);
                    _originalUsbStorStartValue = orig is int origVal ? origVal : 3;
                }
                _simulatedStorage["USBSTOR_Start"] = value;
            }

            return Task.CompletedTask;
        }

        public bool IsHardwareIdAllowed(string hardwareId)
        {
            _logger.LogDebug("Querying Hardware ID: {HardwareId}", hardwareId);

            if (_blacklistedDevices.Contains(hardwareId)) return false;
            if (_whitelistedDevices.Contains(hardwareId)) return true;
            if (_approvedDevices.Count > 0 && !_approvedDevices.Contains(hardwareId)) return false;

            return true;
        }

        public int GetUsbStorStartForTest()
        {
            if (!IsTestOrNonWindows())
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\USBSTOR");
                return (int)(key?.GetValue("Start") ?? 3);
            }
            else
            {
                _simulatedStorage.TryGetValue("USBSTOR_Start", out var val);
                return val is int startVal ? startVal : 3;
            }
        }
    }
}
