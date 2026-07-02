using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SayraClient;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly NetworkManager _networkManager;

    public Worker(ILogger<Worker> logger, NetworkManager networkManager)
    {
        _logger = logger;
        _networkManager = networkManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sayra Client Service starting at: {time}", DateTimeOffset.Now);

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
