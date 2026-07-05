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
        SetTaskManagerEnabled(false);
        _isLocked = true;
    }

    public void Unlock()
    {
        _logger.LogInformation("Disabling Kiosk/Lockdown mode...");
        SetTaskManagerEnabled(true);
        _isLocked = false;
    }

    private void SetTaskManagerEnabled(bool enabled)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning("Task Manager restriction is only supported on Windows.");
            return;
        }

        try
        {
            const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
            const string valueName = "DisableTaskMgr";

            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);

            // If the key doesn't exist and we want to enable (remove restriction), nothing to do
            if (key == null && enabled) return;

            // If the key doesn't exist and we want to disable (add restriction), create it
            if (key == null && !enabled)
            {
                using var newKey = Registry.CurrentUser.CreateSubKey(keyPath);
                newKey.SetValue(valueName, 1, RegistryValueKind.DWord);
                _logger.LogInformation("Task Manager disabled (created key).");
                return;
            }

            if (key != null)
            {
                var currentValue = key.GetValue(valueName);
                if (enabled)
                {
                    if (currentValue != null)
                    {
                        key.DeleteValue(valueName);
                        _logger.LogInformation("Task Manager enabled (removed restriction).");
                    }
                }
                else
                {
                    if (currentValue == null || (int)currentValue != 1)
                    {
                        key.SetValue(valueName, 1, RegistryValueKind.DWord);
                        _logger.LogInformation("Task Manager disabled (set restriction).");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to modify Task Manager registry key.");
        }
    }
}
