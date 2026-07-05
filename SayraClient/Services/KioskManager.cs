using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace SayraClient.Services;

public class KioskManager
{
    private readonly ILogger<KioskManager> _logger;

    private bool _isLocked;

    public KioskManager(ILogger<KioskManager> logger)
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

        // One way to disable PowerShell via registry is to set execution policy to Restricted or use Software Restriction Policies
        // For simplicity in this kiosk agent, we'll set the execution policy for the current user
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
}
