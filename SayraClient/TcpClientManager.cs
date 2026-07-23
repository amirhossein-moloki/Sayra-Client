using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SayraClient.Services;
using Sayra.Client.Discovery.Services;
using Sayra.Client.Discovery.Models;

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
    private readonly IDiscoveryService _discoveryService;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private string? _resolvedIp;
    private int? _resolvedPort;

    public TcpClientManager(
        ILogger<TcpClientManager> logger,
        IConfiguration configuration,
        ReconnectManager reconnectManager,
        MessageHandler messageHandler,
        IServiceProvider serviceProvider,
        SecureTransportLayer transportLayer,
        SessionKeyManager sessionKeyManager,
        AuthManager authManager,
        ClientStateManager stateManager,
        IDiscoveryService discoveryService)
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
        _discoveryService = discoveryService;
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
                    await ResolveAndConnectAsync(cancellationToken);
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
                // Clear resolved endpoint on failure to trigger re-discovery/cache reload
                _resolvedIp = null;
                _resolvedPort = null;
                await _reconnectManager.WaitForNextRetry(cancellationToken);
            }
        }
    }

    private async Task ResolveAndConnectAsync(CancellationToken cancellationToken)
    {
        // 1. Try static IP if configured
        string? staticIp = _configuration["ServerConfig:IpAddress"];
        int staticPort = int.Parse(_configuration["ServerConfig:Port"] ?? "5000");

        if (!string.IsNullOrEmpty(staticIp) && staticIp != "SAYRA_SERVER_IP")
        {
            _resolvedIp = staticIp;
            _resolvedPort = staticPort;
        }
        else
        {
            // 2. Try Discovery (Check Cache first)
            bool discoveryEnabled = _configuration.GetValue<bool>("ServerDiscovery:Enabled", true);
            if (discoveryEnabled)
            {
                _stateManager.TransitionTo(ClientState.DISCOVERING_SERVER);
                var response = await _discoveryService.DiscoverAsync(cancellationToken, forceFresh: false);
                if (response != null)
                {
                    _resolvedIp = response.ip;
                    _resolvedPort = response.tcpPort;
                }
            }
        }

        if (string.IsNullOrEmpty(_resolvedIp))
        {
            _logger.LogWarning("Server IP not resolved. Retrying...");
            return;
        }

        // 3. Try Connect
        bool connected = await ConnectAsync(_resolvedIp, _resolvedPort ?? staticPort, cancellationToken);

        // 4. If connection failed and discovery is enabled, try fresh discovery
        if (!connected && _configuration.GetValue<bool>("ServerDiscovery:Enabled", true) &&
            (string.IsNullOrEmpty(staticIp) || staticIp == "SAYRA_SERVER_IP"))
        {
            _logger.LogInformation("Connection to cached/resolved server failed. Retrying with fresh discovery...");
            _stateManager.TransitionTo(ClientState.DISCOVERING_SERVER);
            var response = await _discoveryService.DiscoverAsync(cancellationToken, forceFresh: true);
            if (response != null)
            {
                _resolvedIp = response.ip;
                _resolvedPort = response.tcpPort;
                await ConnectAsync(_resolvedIp, _resolvedPort ?? staticPort, cancellationToken);
            }
        }
    }

    private async Task<bool> ConnectAsync(string ip, int port, CancellationToken cancellationToken)
    {
        try
        {
            _stateManager.TransitionTo(ClientState.CONNECTING);
            _logger.LogInformation("Attempting to connect to {ip}:{port}...", ip, port);
            _client = new TcpClient();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            await _client.ConnectAsync(ip, port, timeoutCts.Token);
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

            // Await the message loop here to keep the connection alive
            await ReceiveMessagesLoopAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to connect to {ip}:{port} - {message}", ip, port, ex.Message);
            return false;
        }
    }

    public void Disconnect()
    {
        _logger.LogWarning("Manually triggering connection disconnect.");
        CleanupConnection();
    }

    private void CleanupConnection()
    {
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
    }

    private async Task ReceiveMessagesLoopAsync(CancellationToken cancellationToken)
    {
        if (_stream == null) return;

        using var reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
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

        // When loop breaks, trigger reconnect
        _stateManager.TransitionTo(ClientState.DISCONNECTED);
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

    public bool IsConnected => _client is { Connected: true } && _stream != null;

    public async Task<bool> SendMessageAsync(object message, CancellationToken cancellationToken)
    {
        try
        {
            if (_client is { Connected: true } && _stream != null)
            {
                string wrappedJson = _transportLayer.Wrap(message) + "\n";
                byte[] data = Encoding.UTF8.GetBytes(wrappedJson);
                await _stream.WriteAsync(data, 0, data.Length, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message.");
            return false;
        }
    }
}
