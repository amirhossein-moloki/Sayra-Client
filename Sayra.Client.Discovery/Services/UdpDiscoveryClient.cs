using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sayra.Client.Discovery.Models;

namespace Sayra.Client.Discovery.Services;

public class UdpDiscoveryClient
{
    private readonly ILogger<UdpDiscoveryClient> _logger;
    private readonly int _udpPort;

    public UdpDiscoveryClient(ILogger<UdpDiscoveryClient> logger, int udpPort)
    {
        _logger = logger;
        _udpPort = udpPort;
    }

    public virtual async Task<List<ServerDiscoveryResponse>> BroadcastDiscoveryAsync(string clientId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var responses = new List<ServerDiscoveryResponse>();
        using var udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;

        var request = new DiscoveryRequest
        {
            clientId = clientId,
            timestamp = DateTime.UtcNow.ToString("O"),
            nonce = Guid.NewGuid().ToString()
        };

        byte[] requestData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        var endPoint = new IPEndPoint(IPAddress.Broadcast, _udpPort);

        _logger.LogInformation("Broadcasting discovery request to port {port}...", _udpPort);
        await udpClient.SendAsync(requestData, requestData.Length, endPoint);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var receiveTask = udpClient.ReceiveAsync(cts.Token);
                var result = await receiveTask;
                long latency = sw.ElapsedMilliseconds;

                string jsonResponse = Encoding.UTF8.GetString(result.Buffer);
                var response = JsonSerializer.Deserialize<ServerDiscoveryResponse>(jsonResponse);

                if (response != null)
                {
                    response.Latency = latency;
                    responses.Add(response);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error receiving discovery response: {message}", ex.Message);
            }
        }

        return responses;
    }
}
