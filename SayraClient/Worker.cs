using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SayraClient;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly TcpClientManager _networkManager;
    private readonly SayraClient.Services.KioskManager _kioskManager;

    public Worker(
        ILogger<Worker> logger,
        TcpClientManager networkManager,
        SayraClient.Services.KioskManager kioskManager)
    {
        _logger = logger;
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
