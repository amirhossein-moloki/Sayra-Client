using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Services.Windows;

public class PowerStatusChangeHandler : SupervisedBackgroundService
{
    private readonly IAuditLogger _auditLogger;
    private readonly TcpClientManager _tcpClientManager;
    private readonly IWorkstationBackupService _backupService;

    public event Action<PowerModes>? PowerModeChanged;

    public PowerStatusChangeHandler(
        ILogger<PowerStatusChangeHandler> logger,
        IServiceHealthMonitor healthMonitor,
        IAuditLogger auditLogger,
        TcpClientManager tcpClientManager,
        IWorkstationBackupService backupService)
        : base(logger, healthMonitor, "PowerStatusChangeHandler")
    {
        _auditLogger = auditLogger;
        _tcpClientManager = tcpClientManager;
        _backupService = backupService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PowerStatusChangeHandler background service starting...");

        if (OperatingSystem.IsWindows())
        {
            try
            {
                SystemEvents.PowerModeChanged += OnPowerModeChanged;
                _logger.LogInformation("Successfully subscribed to Microsoft.Win32.SystemEvents.PowerModeChanged events.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to Microsoft.Win32.SystemEvents.PowerModeChanged.");
            }
        }
        else
        {
            _logger.LogWarning("PowerStatusChangeHandler power state tracking is only fully supported on Windows. Using simulated/passive mode.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            _healthMonitor.ReportHeartbeat(_serviceName);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        HandlePowerModeChange(e.Mode);
    }

    public void HandlePowerModeChange(PowerModes mode)
    {
        _logger.LogInformation("Windows Power Mode Change detected: {Mode}", mode);

        string logMessage = $"System power status changed to {mode}.";
        _auditLogger.LogOperational(logMessage);

        if (mode == PowerModes.Suspend)
        {
            _auditLogger.LogSecurity("System going to Suspend (low power) state. Initiating configuration save and local queue flush.");

            // Flush configuration and state to disk
            try
            {
                string destPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Backups", "suspend_state.zip");
                // Ensure Backups directory exists
                string dir = System.IO.Path.GetDirectoryName(destPath) ?? "";
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
                _backupService.CreateBackupAsync(destPath, null, CancellationToken.None).GetAwaiter().GetResult();
                _logger.LogInformation("Successfully flushed active workstation configuration on Suspend.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute active configuration flush during Suspend power transition.");
            }
        }
        else if (mode == PowerModes.Resume)
        {
            _auditLogger.LogSecurity("System Resumed from low power state. Forcing immediate server reconnect state check.");

            // Force immediate connection refresh / reconnection bypass
            try
            {
                _tcpClientManager.Disconnect();
                _logger.LogInformation("Triggered immediate TCP Client reconnect on Resume.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger TCP Client disconnect/reconnect on Resume.");
            }
        }

        PowerModeChanged?.Invoke(mode);
    }

    public override void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            }
            catch
            {
                // Suppress event unsubscribe exceptions on teardown
            }
        }
        base.Dispose();
    }
}
