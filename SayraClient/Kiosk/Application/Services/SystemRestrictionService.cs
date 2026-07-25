using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.Kiosk.Domain.Models;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Kiosk.Application.Services;

public class SystemRestrictionService : ISystemRestrictionService, IDisposable
{
    private readonly IAuditLogger _auditLogger;
    private readonly IKioskPolicyService _policyService;
    private bool _isActive;
    private CancellationTokenSource? _monitorCts;
    private readonly HashSet<string> _blockedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "taskmgr",
        "cmd",
        "powershell",
        "pwsh",
        "control",
        "SystemSettings",
        "regedit"
    };

    public SystemRestrictionService(IAuditLogger auditLogger, IKioskPolicyService policyService)
    {
        _auditLogger = auditLogger;
        _policyService = policyService;
    }

    public void EnableSystemRestrictions()
    {
        if (_isActive) return;
        _isActive = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetRegistryPolicies(true);
        }

        _monitorCts = new CancellationTokenSource();
        Task.Run(() => MonitorProcessesAsync(_monitorCts.Token));

        _auditLogger.LogSecurity("[Kiosk Security] System utility restrictions enabled.");
    }

    public void DisableSystemRestrictions()
    {
        if (!_isActive) return;
        _isActive = false;

        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetRegistryPolicies(false);
        }

        _auditLogger.LogSecurity("[Kiosk Security] System utility restrictions disabled.");
    }

    public bool IsSystemRestrictionActive() => _isActive;

    public bool IsProcessBlocked(string processName)
    {
        var cleanName = Path.GetFileNameWithoutExtension(processName);
        return _blockedProcesses.Contains(cleanName);
    }

    private void SetRegistryPolicies(bool enabled)
    {
        int value = enabled ? 1 : 0;
        SetRegistryValue(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr", value);
        SetRegistryValue(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableRegistryTools", value);
        SetRegistryValue(@"Software\Policies\Microsoft\Windows\System", "DisableCMD", value);
        SetRegistryValue(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoControlPanel", value);
    }

    private void SetRegistryValue(string keyPath, string valueName, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, true);
            if (key != null)
            {
                if (value == 0)
                {
                    key.DeleteValue(valueName, false);
                }
                else
                {
                    key.SetValue(valueName, value, RegistryValueKind.DWord);
                }
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperational($"Failed to set registry policy {valueName} in {keyPath}: {ex.Message}");
        }
    }

    private async Task MonitorProcessesAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_policyService.IsRestrictionEnabled(RestrictionType.System))
                {
                    foreach (var procName in _blockedProcesses)
                    {
                        var processes = Process.GetProcessesByName(procName);
                        foreach (var p in processes)
                        {
                            try
                            {
                                p.Kill(true);
                                _auditLogger.LogSecurity($"[Kiosk Security] Unauthorized utility launch blocked: {procName}.exe terminated at {DateTime.UtcNow}");
                            }
                            catch (Exception ex)
                            {
                                _auditLogger.LogOperational($"Failed to terminate blocked process {procName}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _auditLogger.LogOperational($"Error in process monitor loop: {ex.Message}");
            }

            try
            {
                await Task.Delay(1000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        DisableSystemRestrictions();
    }
}
