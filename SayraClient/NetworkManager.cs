using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SayraClient;

public class NetworkManager
{
    private readonly ILogger<NetworkManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly ReconnectManager _reconnectManager;
    private readonly MessageHandler _messageHandler;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly string _ipAddress;
    private readonly int _port;
    private readonly int _heartbeatIntervalSeconds;

    public NetworkManager(
        ILogger<NetworkManager> logger,
        IConfiguration configuration,
        ReconnectManager reconnectManager,
        MessageHandler messageHandler)
    {
        _logger = logger;
        _configuration = configuration;
        _reconnectManager = reconnectManager;
        _messageHandler = messageHandler;

        _ipAddress = _configuration["ServerConfig:IpAddress"] ?? "127.0.0.1";
        _port = int.Parse(_configuration["ServerConfig:Port"] ?? "5000");
        _heartbeatIntervalSeconds = int.Parse(_configuration["ServerConfig:HeartbeatIntervalSeconds"] ?? "10");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NetworkManager starting...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_client == null || !_client.Connected)
                {
                    _logger.LogInformation("Attempting to connect to {ip}:{port}...", _ipAddress, _port);
                    _client = new TcpClient();
                    await _client.ConnectAsync(_ipAddress, _port, cancellationToken);
                    _stream = _client.GetStream();
                    _reconnectManager.Reset();
                    _logger.LogInformation("Connected to server.");

                    // Start background tasks for heartbeats and receiving messages
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var heartbeatTask = SendHeartbeatLoopAsync(cts.Token);
                    var receiveTask = ReceiveMessagesLoopAsync(cts.Token);

                    // Wait for either the token to be cancelled or one of the tasks to fail
                    await Task.WhenAny(heartbeatTask, receiveTask);

                    // Signal cancellation to the other task if one of them stopped
                    cts.Cancel();
                    await Task.WhenAll(heartbeatTask, receiveTask);
                }
            }
            catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Connection lost or failed: {message}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in NetworkManager.");
            }
            finally
            {
                CleanupConnection();
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await _reconnectManager.WaitForNextRetry(cancellationToken);
            }
        }
    }

    private void CleanupConnection()
    {
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
    }

    private async Task SendHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var heartbeat = new { type = "HEARTBEAT", timestamp = DateTime.UtcNow };
            await SendMessageAsync(heartbeat, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(_heartbeatIntervalSeconds), cancellationToken);
        }
    }

    private async Task ReceiveMessagesLoopAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(_stream!, Encoding.UTF8, leaveOpen: true);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                _logger.LogInformation("Server closed the connection.");
                break;
            }

            _logger.LogDebug("Received: {message}", line);
            await _messageHandler.HandleMessageAsync(line, this, cancellationToken);
        }
    }

    public async Task SendMessageAsync(object message, CancellationToken cancellationToken)
    {
        if (_client is { Connected: true } && _stream != null)
        {
            string json = JsonSerializer.Serialize(message) + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);
            await _stream.WriteAsync(data, 0, data.Length, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }
    }
}
