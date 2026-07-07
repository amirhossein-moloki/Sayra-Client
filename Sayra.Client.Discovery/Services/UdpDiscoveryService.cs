using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sayra.Client.Discovery.Models;

namespace Sayra.Client.Discovery.Services;

public class UdpDiscoveryService : IDiscoveryService
{
    private readonly ILogger<UdpDiscoveryService> _logger;
    private readonly IConfiguration _configuration;
    private readonly int _discoveryPort;
    private readonly HashSet<string> _seenResponses = new();
    private readonly object _cacheLock = new();

    public UdpDiscoveryService(ILogger<UdpDiscoveryService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _discoveryPort = int.Parse(_configuration["DiscoveryConfig:Port"] ?? "5001");
    }

    public async Task<DiscoveryResponse?> DiscoverAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting server discovery on port {port}...", _discoveryPort);

        using var udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;

        var request = new DiscoveryRequest
        {
            clientId = GetHardwareId(),
            timestamp = DateTime.UtcNow.ToString("O"),
            nonce = Guid.NewGuid().ToString()
        };

        string jsonRequest = JsonSerializer.Serialize(request);
        byte[] data = Encoding.UTF8.GetBytes(jsonRequest);

        await udpClient.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Broadcast, _discoveryPort));

        var responses = new List<(DiscoveryResponse response, long latency)>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var receiveTask = udpClient.ReceiveAsync(cts.Token);
                var result = await receiveTask;
                long latency = sw.ElapsedMilliseconds;

                string jsonResponse = Encoding.UTF8.GetString(result.Buffer);
                var response = JsonSerializer.Deserialize<DiscoveryResponse>(jsonResponse);

                if (response != null && ValidateResponse(response))
                {
                    _logger.LogInformation("Found valid server: {serverId} at {ip}:{port}", response.serverId, response.ip, response.tcpPort);
                    responses.Add((response, latency));
                }
            }
            catch (OperationCanceledException)
            {
                // Expected timeout
                break;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Received malformed discovery response: {message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during UDP discovery message processing.");
            }
        }

        return SelectBestServer(responses);
    }

    private bool ValidateResponse(DiscoveryResponse response)
    {
        try
        {
            // 1. Validate HMAC-SHA256 signature
            string? masterKeyBase64 = _configuration["SAYRA_MASTER_KEY"] ?? _configuration["SecurityConfig:MasterKey"];
            if (string.IsNullOrEmpty(masterKeyBase64) || masterKeyBase64.Contains("PLACEHOLDER"))
            {
                _logger.LogError("MasterKey not configured for discovery validation.");
                return false;
            }

            byte[] masterKey = Convert.FromBase64String(masterKeyBase64);
            string message = $"{response.serverId}|{response.ip}|{response.tcpPort}|{response.timestamp}";

            using var hmac = new HMACSHA256(masterKey);
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            string expectedSignature = Convert.ToBase64String(hash);

            if (response.signature != expectedSignature)
            {
                _logger.LogWarning("Invalid signature from server {serverId}", response.serverId);
                return false;
            }

            // 2. Reject expired timestamps (±10s window)
            if (!DateTime.TryParse(response.timestamp, out var serverTime))
            {
                return false;
            }

            if (Math.Abs((DateTime.UtcNow - serverTime.ToUniversalTime()).TotalSeconds) > 10)
            {
                _logger.LogWarning("Expired timestamp from server {serverId}", response.serverId);
                return false;
            }

            // 3. Reject replayed responses (using signature as unique identifier for the response)
            lock (_cacheLock)
            {
                if (_seenResponses.Contains(response.signature))
                {
                    _logger.LogWarning("Replayed response detected from server {serverId}", response.serverId);
                    return false;
                }
                _seenResponses.Add(response.signature);
                // Keep cache small - simple LRU-ish eviction for HashSet in this context
                if (_seenResponses.Count > 100)
                {
                    var enumerator = _seenResponses.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        _seenResponses.Remove(enumerator.Current);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating discovery response.");
            return false;
        }
    }

    private DiscoveryResponse? SelectBestServer(List<(DiscoveryResponse response, long latency)> responses)
    {
        if (responses.Count == 0) return null;

        // Priority rules:
        // 1. Highest compatible version match
        // 2. Valid signature only (already filtered)
        // 3. Lowest latency response

        return responses
            .OrderByDescending(r => ParseVersion(r.response.version))
            .ThenBy(r => r.latency)
            .Select(r => r.response)
            .FirstOrDefault();
    }

    private Version ParseVersion(string versionStr)
    {
        if (Version.TryParse(versionStr, out var version))
        {
            return version;
        }
        return new Version(0, 0);
    }

    private string GetHardwareId()
    {
        // Simple hardware-bound ID for now. In production, use more robust methods.
        return Environment.MachineName;
    }
}
