using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SayraClient.Services;

namespace SayraClient;

public class TcpClientManager
{
    private readonly ILogger<TcpClientManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly ReconnectManager _reconnectManager;
    private readonly MessageHandler _messageHandler;
    private readonly IServiceProvider _serviceProvider;
    private readonly SecureTransportLayer _transportLayer;
    private readonly SessionKeyManager _sessionKeyManager;
    private readonly AuthManager _authManager;
    private readonly ClientStateManager _stateManager;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly string _ipAddress;
    private readonly int _port;

    public TcpClientManager(
        ILogger<TcpClientManager> logger,
        IConfiguration configuration,
        ReconnectManager reconnectManager,
        MessageHandler messageHandler,
        IServiceProvider serviceProvider,
        SecureTransportLayer transportLayer,
        SessionKeyManager sessionKeyManager,
        AuthManager authManager,
        ClientStateManager stateManager)
    {
        _logger = logger;
        _configuration = configuration;
        _reconnectManager = reconnectManager;
        _messageHandler = messageHandler;
        _serviceProvider = serviceProvider;
        _transportLayer = transportLayer;
        _sessionKeyManager = sessionKeyManager;
        _authManager = authManager;
        _stateManager = stateManager;

        _ipAddress = _configuration["ServerConfig:IpAddress"] ?? "127.0.0.1";
        _port = int.Parse(_configuration["ServerConfig:Port"] ?? "5000");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TcpClientManager starting...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_client == null || !_client.Connected)
                {
                    _stateManager.TransitionTo(ClientState.CONNECTING);
                    _logger.LogInformation("Attempting to connect to {ip}:{port}...", _ipAddress, _port);
                    _client = new TcpClient();
                    await _client.ConnectAsync(_ipAddress, _port, cancellationToken);
                    _stream = _client.GetStream();
                    _reconnectManager.Reset();
                    _logger.LogInformation("Connected to server.");

                    if (!_sessionKeyManager.IsAuthenticated)
                    {
                        _stateManager.TransitionTo(ClientState.AUTHENTICATING);
                    }
                    else
                    {
                        _stateManager.TransitionTo(ClientState.READY);
                        await SendStateSyncAsync(cancellationToken);
                    }

                    // Start background task for receiving messages
                    await ReceiveMessagesLoopAsync(cancellationToken);
                }
            }
            catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Connection lost or failed: {message}", ex.Message);
                    _stateManager.TransitionTo(ClientState.DISCONNECTED);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TcpClientManager.");
                _stateManager.TransitionTo(ClientState.DISCONNECTED);
            }
            finally
            {
                CleanupConnection();
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                _stateManager.TransitionTo(ClientState.RECOVERING);
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
        // Keep session key for now to support reconnection without re-auth if server allows
        // _sessionKeyManager.ClearSessionKey();
        // _authManager.Reset();
    }

    private async Task ReceiveMessagesLoopAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(_stream!, Encoding.UTF8, leaveOpen: true);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    _logger.LogInformation("Server closed the connection.");
                    break;
                }

                _logger.LogDebug("Received raw message.");
                if (string.IsNullOrWhiteSpace(line)) continue;
                await _messageHandler.HandleMessageAsync(line, this, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error receiving or handling message.");
                if (ex is IOException or SocketException)
                {
                    break;
                }
            }
        }
    }

    public async Task SendStateSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var sessionManager = scope.ServiceProvider.GetRequiredService<SayraClient.Services.SessionManager>();
            var currentSession = sessionManager.GetCurrentSession();

            await SendMessageAsync(new {
                type = "EVENT",
                @event = "CLIENT_CONNECTED",
                timestamp = DateTime.UtcNow,
                session = currentSession
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending state sync.");
        }
    }

    public async Task SendMessageAsync(object message, CancellationToken cancellationToken)
    {
        try
        {
            if (_client is { Connected: true } && _stream != null)
            {
                string wrappedJson = _transportLayer.Wrap(message) + "\n";
                byte[] data = Encoding.UTF8.GetBytes(wrappedJson);
                await _stream.WriteAsync(data, 0, data.Length, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message.");
        }
    }
}
