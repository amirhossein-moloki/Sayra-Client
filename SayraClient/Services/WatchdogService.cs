using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class WatchdogService : BackgroundService
{
    private readonly ILogger<WatchdogService> _logger;
    private readonly RecoveryManager _recoveryManager;

    public WatchdogService(ILogger<WatchdogService> logger, RecoveryManager recoveryManager)
    {
        _logger = logger;
        _recoveryManager = recoveryManager;
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
                // In a production environment, this could monitor process health,
                // check for unauthorized changes to system settings, etc.

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
}
