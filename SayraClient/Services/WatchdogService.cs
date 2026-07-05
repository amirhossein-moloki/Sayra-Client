using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class WatchdogService : BackgroundService
{
    private readonly ILogger<WatchdogService> _logger;
    private readonly RecoveryManager _recoveryManager;
    private readonly TcpClientManager _networkManager;

    public WatchdogService(
        ILogger<WatchdogService> logger,
        RecoveryManager recoveryManager,
        TcpClientManager networkManager)
    {
        _logger = logger;
        _recoveryManager = recoveryManager;
        _networkManager = networkManager;
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
                EnsureGuardianRunning();

                _logger.LogDebug("Watchdog performing health check...");

                // For now, we'll just periodically ensure state is as expected
                _recoveryManager.RecoverState();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Watchdog health check.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
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
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = guardianPath,
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
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
