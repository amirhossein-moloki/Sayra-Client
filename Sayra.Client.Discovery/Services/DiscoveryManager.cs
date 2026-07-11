using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sayra.Client.Discovery.Models;

namespace Sayra.Client.Discovery.Services;

public class DiscoveryManager : IDiscoveryService
{
    private readonly ILogger<DiscoveryManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly UdpDiscoveryClient _udpClient;
    private readonly DiscoveryValidator _validator;
    private readonly string _cachePath = "server_cache.json";

    public DiscoveryManager(
        ILogger<DiscoveryManager> logger,
        IConfiguration configuration,
        UdpDiscoveryClient udpClient,
        DiscoveryValidator validator)
    {
        _logger = logger;
        _configuration = configuration;
        _udpClient = udpClient;
        _validator = validator;
    }

    public async Task<DiscoveryResponse?> DiscoverAsync(CancellationToken cancellationToken, bool forceFresh = false)
    {
        var server = await InternalDiscoverAsync(cancellationToken, forceFresh);
        if (server == null) return null;

        return new DiscoveryResponse
        {
            serverId = server.serverId,
            ip = server.ip,
            tcpPort = server.tcpPort,
            type = server.type,
            timestamp = server.timestamp,
            signature = server.signature,
            serverName = server.serverName,
            version = server.version,
            apiPort = server.apiPort,
            nonce = server.nonce
        };
    }

    private async Task<ServerDiscoveryResponse?> InternalDiscoverAsync(CancellationToken cancellationToken, bool forceFresh)
    {
        _logger.LogInformation("Starting LAN server discovery");

        // 1. Check Cache
        var cachedServer = forceFresh ? null : LoadCache();
        if (cachedServer != null)
        {
            _logger.LogInformation("Found cached server: {serverId} at {ip}", cachedServer.ServerId, cachedServer.LastIPAddress);
            // We still need to return a ServerDiscoveryResponse.
            // In a real scenario, we might want to verify if the cached server is still alive via TCP first.
            // But here the flow says: Check Cache -> Try TCP -> If failed: Start UDP.
            // TcpClientManager handles the Try TCP part.

            return new ServerDiscoveryResponse
            {
                serverId = cachedServer.ServerId,
                serverName = cachedServer.ServerName,
                ip = cachedServer.LastIPAddress,
                tcpPort = cachedServer.TcpPort
            };
        }

        // 2. UDP Discovery
        int timeoutSec = int.Parse(_configuration["ServerDiscovery:DiscoveryTimeoutSeconds"] ?? "5");
        var responses = await _udpClient.BroadcastDiscoveryAsync(Environment.MachineName, TimeSpan.FromSeconds(timeoutSec), cancellationToken);

        // 3. Validate Responses
        var validResponses = responses.Where(r => _validator.Validate(r)).ToList();

        if (validResponses.Count == 0)
        {
            _logger.LogError("No trusted Sayra Server found");
            return null;
        }

        // 4. Select Best Server
        string? trustedServerId = null;
        var cached = LoadCache();
        if (cached != null)
        {
            trustedServerId = cached.ServerId;
        }
        var selectedServer = SelectBestServer(validResponses, trustedServerId);

        if (selectedServer != null)
        {
            _logger.LogInformation("Sayra Server discovered: {serverId} at {ip}:{port}", selectedServer.serverId, selectedServer.ip, selectedServer.tcpPort);
            _logger.LogInformation("Server identity verified");
            SaveCache(selectedServer);
        }

        return selectedServer;
    }

    private ServerDiscoveryResponse? SelectBestServer(List<ServerDiscoveryResponse> responses, string? trustedServerId)
    {
        if (responses.Count == 0) return null;

        // Priority:
        // 1. Previously trusted ServerId (if we had one)
        // 2. Valid signature (already filtered)
        // 3. Lowest latency

        if (!string.IsNullOrEmpty(trustedServerId))
        {
            var trustedMatch = responses
                .Where(r => r.serverId == trustedServerId)
                .OrderBy(r => r.Latency)
                .FirstOrDefault();

            if (trustedMatch != null)
            {
                return trustedMatch;
            }
        }

        return responses.OrderBy(r => r.Latency).FirstOrDefault();
    }

    private ServerCache? LoadCache()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                string json = File.ReadAllText(_cachePath);
                return JsonSerializer.Deserialize<ServerCache>(json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to load server cache: {message}", ex.Message);
        }
        return null;
    }

    private void SaveCache(ServerDiscoveryResponse response)
    {
        try
        {
            var cache = new ServerCache
            {
                ServerId = response.serverId,
                ServerName = response.serverName,
                LastIPAddress = response.ip,
                TcpPort = response.tcpPort,
                LastConnected = DateTime.UtcNow,
                // PublicKeyFingerprint could be added here
            };
            string json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cachePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to save server cache: {message}", ex.Message);
        }
    }

    public void ClearCache()
    {
        if (File.Exists(_cachePath))
        {
            File.Delete(_cachePath);
        }
    }
}
