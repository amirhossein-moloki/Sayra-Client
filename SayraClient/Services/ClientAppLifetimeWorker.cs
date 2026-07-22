using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

/// <summary>
/// Orchestrates the client's global lifetime, executing the startup pipeline and handling unexpected failures.
/// </summary>
public class ClientAppLifetimeWorker : BackgroundService
{
    private readonly ILogger<ClientAppLifetimeWorker> _logger;
    private readonly IStartupPipeline _startupPipeline;
    private readonly IShutdownCoordinator _shutdownCoordinator;

    public ClientAppLifetimeWorker(
        ILogger<ClientAppLifetimeWorker> logger,
        IStartupPipeline startupPipeline,
        IShutdownCoordinator shutdownCoordinator)
    {
        _logger = logger;
        _startupPipeline = startupPipeline;
        _shutdownCoordinator = shutdownCoordinator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClientAppLifetimeWorker running. Launching 10-Stage Startup Pipeline...");

        try
        {
            await _startupPipeline.ExecuteAsync(stoppingToken);
            _logger.LogInformation("SAYRA Enterprise Client is fully operational. Running background processing loops...");

            // Keep the background service alive until the host triggers cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ClientAppLifetimeWorker execution cancellation received.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "SAYRA Client Host encountered a fatal unhandled startup exception.");
            await _shutdownCoordinator.InitiateShutdownAsync($"Fatal Startup Error: {ex.Message}", -1);
        }
    }
}
