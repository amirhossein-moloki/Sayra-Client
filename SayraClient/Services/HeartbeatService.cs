using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class HeartbeatService : BackgroundService
{
    private readonly ILogger<HeartbeatService> _logger;
    private readonly TcpClientManager _tcpClientManager;
    private readonly ClientStateManager _stateManager;
    private readonly int _intervalSeconds;

    public HeartbeatService(
        ILogger<HeartbeatService> logger,
        TcpClientManager tcpClientManager,
        ClientStateManager stateManager,
        IConfiguration configuration)
    {
        _logger = logger;
        _tcpClientManager = tcpClientManager;
        _stateManager = stateManager;
        _intervalSeconds = int.Parse(configuration["ServerConfig:HeartbeatIntervalSeconds"] ?? "10");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatService starting with interval: {interval}s", _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_stateManager.IsReady())
                {
                    var heartbeat = new
                    {
                        type = "HEARTBEAT",
                        timestamp = DateTime.UtcNow
                    };
                    await _tcpClientManager.SendMessageAsync(heartbeat, stoppingToken);
                    _logger.LogDebug("Heartbeat sent.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error sending heartbeat.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }
}
