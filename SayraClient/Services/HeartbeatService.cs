using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class HeartbeatService : SupervisedBackgroundService
{
    private readonly IHeartbeatManager _heartbeatManager;

    public HeartbeatService(
        ILogger<HeartbeatService> logger,
        IHeartbeatManager heartbeatManager,
        IServiceHealthMonitor healthMonitor)
        : base(logger, healthMonitor, "HeartbeatService")
    {
        _heartbeatManager = heartbeatManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Heartbeat Service starting and delegating to Heartbeat Manager...");

        // Start heartbeat manager
        await _heartbeatManager.StartAsync(stoppingToken);

        try
        {
            // Just await cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Heartbeat Service is stopping.");
        }
        finally
        {
            await _heartbeatManager.StopAsync();
        }
    }
}
