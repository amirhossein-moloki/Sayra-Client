using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Services.Windows;

public class RegistryWatcher : SupervisedBackgroundService
{
    private readonly KioskManager _kioskManager;
    private readonly IPowerManagementService _powerManagementService;
    private readonly IAuditLogger _auditLogger;
    private readonly IWindowsEventLogService _eventLogService;
    private const string WinlogonKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";
    private const string ExpectedShellValue = "SayraClient.exe";

    public RegistryWatcher(
        ILogger<RegistryWatcher> logger,
        IServiceHealthMonitor healthMonitor,
        KioskManager kioskManager,
        IPowerManagementService powerManagementService,
        IAuditLogger auditLogger,
        IWindowsEventLogService eventLogService)
        : base(logger, healthMonitor, "RegistryWatcher")
    {
        _kioskManager = kioskManager;
        _powerManagementService = powerManagementService;
        _auditLogger = auditLogger;
        _eventLogService = eventLogService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RegistryWatcher service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _healthMonitor.ReportHeartbeat(_serviceName);

            if (OperatingSystem.IsWindows() && _kioskManager.IsLocked())
            {
                await CheckRegistryPoliciesAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task CheckRegistryPoliciesAsync(CancellationToken ct)
    {
        try
        {
            bool tampered = false;
            string tamperingDetails = "";

            // 1. Check Winlogon Shell Key (Using CreateSubKey to ensure key is created and enforced if missing)
            using (var key = Registry.CurrentUser.CreateSubKey(WinlogonKeyPath, true))
            {
                var shellVal = key.GetValue("Shell") as string;
                if (shellVal == null || !shellVal.Equals(ExpectedShellValue, StringComparison.OrdinalIgnoreCase))
                {
                    tampered = true;
                    tamperingDetails += $"Shell modified from {ExpectedShellValue} to {shellVal ?? "NULL"}. ";
                    key.SetValue("Shell", ExpectedShellValue, RegistryValueKind.String);
                }
            }

            // 2. Check Task Manager Policy (Using CreateSubKey to prevent bypass if registry key path is missing/deleted)
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", true))
            {
                var disableTaskMgr = key.GetValue("DisableTaskMgr");
                if (disableTaskMgr == null || Convert.ToInt32(disableTaskMgr) != 1)
                {
                    tampered = true;
                    tamperingDetails += "DisableTaskMgr policy altered or missing. ";
                    key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                }
            }

            // 3. Check Registry Tools Policy (Using CreateSubKey to prevent bypass if registry key path is missing/deleted)
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", true))
            {
                var disableRegTools = key.GetValue("DisableRegistryTools");
                if (disableRegTools == null || Convert.ToInt32(disableRegTools) != 1)
                {
                    tampered = true;
                    tamperingDetails += "DisableRegistryTools policy altered or missing. ";
                    key.SetValue("DisableRegistryTools", 1, RegistryValueKind.DWord);
                }
            }

            // 4. Check CMD Policy (Using CreateSubKey to prevent bypass if registry key path is missing/deleted)
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\System", true))
            {
                var disableCmd = key.GetValue("DisableCMD");
                if (disableCmd == null || Convert.ToInt32(disableCmd) != 1)
                {
                    tampered = true;
                    tamperingDetails += "DisableCMD policy altered or missing. ";
                    key.SetValue("DisableCMD", 1, RegistryValueKind.DWord);
                }
            }

            if (tampered)
            {
                _logger.LogWarning("SECURITY ALERT: Registry tampering detected! Details: {Details}", tamperingDetails);

                // Revert/Heal policies using KioskManager
                _kioskManager.ReapplyPolicies();

                // Raise security audit event
                _auditLogger.LogSecurity("Registry tampering detected! Unauthorized policy or shell modifications made. Workstation auto-reverted and locked down. Details: " + tamperingDetails);

                // Write to native Windows Event Log
                _eventLogService.WriteEntry(
                    $"SAYRA Kiosk Security Tampering Blocked. Action: Reverted and Locked. Details: {tamperingDetails}",
                    EventLogEntryType.Warning,
                    3001);

                // Force lock workstation
                await _powerManagementService.LockWorkstationAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while auditing registry policies.");
        }
    }
}
