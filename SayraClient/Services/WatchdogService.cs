using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Launcher.Services;

namespace SayraClient.Services;

public class WatchdogService : SupervisedBackgroundService
{
    private readonly RecoveryManager _recoveryManager;
    private readonly TcpClientManager _networkManager;
    private readonly IGameLauncherService _gameLauncher;

    public WatchdogService(
        ILogger<WatchdogService> logger,
        RecoveryManager recoveryManager,
        TcpClientManager networkManager,
        IGameLauncherService gameLauncher,
        IServiceHealthMonitor healthMonitor)
        : base(logger, healthMonitor, "WatchdogService")
    {
        _recoveryManager = recoveryManager;
        _networkManager = networkManager;
        _gameLauncher = gameLauncher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Watchdog Service starting...");

        // Initial recovery on startup
        _recoveryManager.RecoverState();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _healthMonitor.ReportHeartbeat("WatchdogService");

                EnsureGuardianRunning();

                _logger.LogDebug("Watchdog performing health check...");

                // For now, we'll just periodically ensure state is as expected
                _recoveryManager.RecoverState();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Watchdog health check.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Watchdog Service stopping.");
    }

    private void EnsureGuardianRunning()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("Sayra.Client.Guardian");
            if (processes.Length == 0)
            {
                _logger.LogWarning("Sayra Guardian process not found! Restarting...");

                string guardianPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Sayra.Client.Guardian.exe");
                if (System.IO.File.Exists(guardianPath))
                {
                    _ = _gameLauncher.LaunchApplicationAsync(guardianPath, "", "", false, CancellationToken.None);
                }
                else
                {
                    _logger.LogError("Sayra Guardian executable not found at {Path}", guardianPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring Guardian is running.");
        }
    }
}
