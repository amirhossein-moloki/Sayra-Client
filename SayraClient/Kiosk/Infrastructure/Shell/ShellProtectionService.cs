using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SayraClient.Kiosk.Application.Interfaces;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Kiosk.Infrastructure.Shell;

public class ShellProtectionService : IShellProtectionService
{
    private readonly IAuditLogger _auditLogger;
    private const string WinlogonKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
    private const string ShellValueName = "Shell";

    public ShellProtectionService(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public bool CheckShellState()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(WinlogonKeyPath);
            if (key != null)
            {
                var value = key.GetValue(ShellValueName) as string;
                return value != null && value.Contains("Sayra.UI.exe", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperational($"Failed to check Winlogon Shell registry key: {ex.Message}");
        }

        return false;
    }

    public bool DetectUnexpectedExplorer()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            var processes = Process.GetProcessesByName("explorer");
            return processes.Length > 0;
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperational($"Failed to query explorer processes: {ex.Message}");
        }

        return false;
    }

    public void RestoreSayraShell()
    {
        _auditLogger.LogAudit("[Kiosk Security] Restoring custom SAYRA shell environment.");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(WinlogonKeyPath, true);
            if (key != null)
            {
                key.SetValue(ShellValueName, "Sayra.UI.exe", RegistryValueKind.String);
            }
        }
        catch (UnauthorizedAccessException)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", true);
                if (key != null)
                {
                    key.SetValue(ShellValueName, "Sayra.UI.exe", RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                _auditLogger.LogOperational($"Failed to set HKCU shell registry policy: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperational($"Failed to set HKLM shell registry policy: {ex.Message}");
        }
    }

    public void RestoreExplorerShell()
    {
        _auditLogger.LogAudit("[Kiosk Security] Restoring default Windows Explorer shell environment.");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(WinlogonKeyPath, true);
            if (key != null)
            {
                key.SetValue(ShellValueName, "explorer.exe", RegistryValueKind.String);
            }
        }
        catch (UnauthorizedAccessException)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", true);
                if (key != null)
                {
                    key.SetValue(ShellValueName, "explorer.exe", RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                _auditLogger.LogOperational($"Failed to restore explorer.exe in HKCU: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperational($"Failed to restore explorer.exe in HKLM: {ex.Message}");
        }
    }
}
