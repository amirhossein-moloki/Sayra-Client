using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Ipc;

namespace SayraClient.Services.Windows;

public class WtsSessionChangeMonitor : SupervisedBackgroundService
{
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IAuditLogger _auditLogger;
    private readonly IpcServer _ipcServer;

    public event Action<SessionSwitchReason>? SessionChanged;

    public WtsSessionChangeMonitor(
        ILogger<WtsSessionChangeMonitor> logger,
        IServiceHealthMonitor healthMonitor,
        IEventDispatcher eventDispatcher,
        IAuditLogger auditLogger,
        IpcServer ipcServer)
        : base(logger, healthMonitor, "WtsSessionChangeMonitor")
    {
        _eventDispatcher = eventDispatcher;
        _auditLogger = auditLogger;
        _ipcServer = ipcServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WtsSessionChangeMonitor background service starting...");

        if (OperatingSystem.IsWindows())
        {
            try
            {
                SystemEvents.SessionSwitch += OnSessionSwitch;
                _logger.LogInformation("Successfully subscribed to Microsoft.Win32.SystemEvents.SessionSwitch events.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to Microsoft.Win32.SystemEvents.SessionSwitch.");
            }
        }
        else
        {
            _logger.LogWarning("WtsSessionChangeMonitor session state tracking is only fully supported on Windows. Using simulated/passive mode.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            _healthMonitor.ReportHeartbeat(_serviceName);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        HandleSessionSwitch(e.Reason);
    }

    public void HandleSessionSwitch(SessionSwitchReason reason)
    {
        _logger.LogInformation("Windows Session Switch event detected: {Reason}", reason);

        // Map reason to dynamic action log template
        string logMessage = $"Windows Session State changed: {reason}.";
        _auditLogger.LogOperational(logMessage);

        // Security logging on lock/unlock or logon/logoff
        if (reason == SessionSwitchReason.SessionLock || reason == SessionSwitchReason.SessionLogoff)
        {
            _auditLogger.LogSecurity($"Session lock or logoff triggered. Workstation state secure. Reason: {reason}");
        }
        else if (reason == SessionSwitchReason.SessionUnlock || reason == SessionSwitchReason.SessionLogon)
        {
            _auditLogger.LogSecurity($"Session unlock or logon triggered. Resuming visual presentation shell. Reason: {reason}");

            // Notify active UI overlays to align or refresh state
            _ = Task.Run(async () =>
            {
                try
                {
                    await _ipcServer.BroadcastEventAsync(IpcMessageType.GET_STATE, new { AlignShell = true, Reason = reason.ToString() });
                    _logger.LogInformation("Broadcasted align-shell IPC request to active UI overlay clients.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast align-shell state trigger over IPC.");
                }
            });
        }

        // Fire local event hooks for any in-process subscribers
        SessionChanged?.Invoke(reason);
    }

    public override void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
            }
            catch
            {
                // Suppress event unsubscribe exceptions on teardown
            }
        }
        base.Dispose();
    }
}
