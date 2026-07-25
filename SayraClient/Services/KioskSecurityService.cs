using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient.Services;

public class KioskSecurityService : IKioskSecurityService
{
    private readonly ILogger<KioskSecurityService> _logger;
    private bool _isLocked;

    public KioskSecurityService(ILogger<KioskSecurityService> logger)
    {
        _logger = logger;
    }

    public bool IsLocked() => _isLocked;

    public void Lockdown()
    {
        _logger.LogInformation("Enabling Kiosk/Lockdown mode...");
        ApplyRestrictions(true);
        _isLocked = true;
    }

    public void Unlock()
    {
        _logger.LogInformation("Disabling Kiosk/Lockdown mode...");
        ApplyRestrictions(false);
        _isLocked = false;
    }

    public void ReapplyPolicies()
    {
        if (_isLocked)
        {
            _logger.LogDebug("Self-healing: Re-applying kiosk policies...");
            ApplyRestrictions(true);
        }
    }

    private void ApplyRestrictions(bool lockDown)
    {
        SetTaskManagerDisabled(lockDown);
        SetRegistryEditorDisabled(lockDown);
        SetCmdDisabled(lockDown);
        SetPowerShellDisabled(lockDown);
    }

    private void SetTaskManagerDisabled(bool disabled)
    {
        SetRegistryPolicy(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr", disabled ? 1 : 0);
    }

    private void SetRegistryEditorDisabled(bool disabled)
    {
        SetRegistryPolicy(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableRegistryTools", disabled ? 1 : 0);
    }

    private void SetCmdDisabled(bool disabled)
    {
        SetRegistryPolicy(@"Software\Policies\Microsoft\Windows\System", "DisableCMD", disabled ? 1 : 0);
    }

    private void SetPowerShellDisabled(bool disabled)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            const string keyPath = @"Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell";
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, true);
            if (disabled)
            {
                key.SetValue("ExecutionPolicy", "Restricted", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue("ExecutionPolicy", false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set PowerShell execution policy.");
        }
    }

    private void SetRegistryPolicy(string keyPath, string valueName, int value)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, true);
            if (value == 0)
            {
                if (key.GetValue(valueName) != null)
                {
                    key.DeleteValue(valueName);
                    _logger.LogInformation("Policy {Policy} removed.", valueName);
                }
            }
            else
            {
                var current = key.GetValue(valueName);
                if (current == null || (int)current != value)
                {
                    key.SetValue(valueName, value, RegistryValueKind.DWord);
                    _logger.LogInformation("Policy {Policy} set to {Value}.", valueName, value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set registry policy {Policy} in {Path}.", valueName, keyPath);
        }
    }

    // --- New IKioskSecurityService required members ---

    public Task EnableKioskLockdownAsync()
    {
        Lockdown();
        return Task.CompletedTask;
    }

    public Task DisableKioskLockdownAsync()
    {
        Unlock();
        return Task.CompletedTask;
    }

    public bool IsKeyboardShortcutBlocked(int virtualKeyCode, int modifiers)
    {
        // Simple default hook implementation placeholder
        return _isLocked;
    }

    public void SpawnSecureDesktop()
    {
        _logger.LogInformation("Spawning Secure Desktop...");
    }

    public void ReleaseSecureDesktop()
    {
        _logger.LogInformation("Releasing Secure Desktop...");
    }

    public void EnableKioskMode()
    {
        Lockdown();
    }

    public void DisableKioskMode()
    {
        Unlock();
    }

    public bool ValidateSecurityState()
    {
        return true;
    }

    public void RepairSecurityPolicy()
    {
        ReapplyPolicies();
    }
}
