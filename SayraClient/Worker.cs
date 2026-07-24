using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SayraClient.Services;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient;

public class Worker : SupervisedBackgroundService
{
    private readonly TcpClientManager _networkManager;
    private readonly IKioskSecurityService _kioskManager;

    public Worker(
        ILogger<Worker> logger,
        TcpClientManager networkManager,
        IKioskSecurityService kioskManager,
        IServiceHealthMonitor healthMonitor)
        : base(logger, healthMonitor, "NetworkWorker")
    {
        _networkManager = networkManager;
        _kioskManager = kioskManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sayra Client Service starting at: {time}", DateTimeOffset.Now);

        // Ensure kiosk mode is active if it was previously locked
        _kioskManager.ReapplyPolicies();

        try
        {
            // Report periodic heartbeat while running
            _ = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _healthMonitor.ReportHeartbeat("NetworkWorker");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }, stoppingToken);

            await _networkManager.StartAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Sayra Client Service is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Sayra Client Service failed.");
            throw;
        }

        _logger.LogInformation("Sayra Client Service stopped at: {time}", DateTimeOffset.Now);
    }
}
