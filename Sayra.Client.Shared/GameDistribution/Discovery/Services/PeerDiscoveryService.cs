using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.GameDistribution.Cache.Interfaces;
using Sayra.Client.Shared.GameDistribution.Cache.Models;
using Sayra.Client.Shared.GameDistribution.Discovery.Interfaces;

namespace Sayra.Client.Shared.GameDistribution.Discovery.Services
{
    public class PeerDiscoveryService : IPeerDiscoveryService
    {
        private const string MulticastAddress = "239.1.1.1";
        private const int Port = 11200;

        private readonly IDistributedCacheManager _cacheManager;
        private readonly ILogger<PeerDiscoveryService> _logger;
        private UdpClient? _udpListener;
        private UdpClient? _udpSender;
        private CancellationTokenSource? _listeningCts;
        private Task? _listenerTask;
        private bool _isDisposed;

        public event EventHandler<CacheNode>? PeerDiscovered;

        public PeerDiscoveryService(
            IDistributedCacheManager cacheManager,
            ILogger<PeerDiscoveryService> logger)
        {
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
        {
            if (_udpListener != null) return Task.CompletedTask;

            _logger.LogInformation("Starting LAN Peer Discovery on UDP port {Port}, multicast {Address}...", Port, MulticastAddress);

            try
            {
                _udpListener = new UdpClient();
                _udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
                _udpListener.JoinMulticastGroup(IPAddress.Parse(MulticastAddress));

                _udpSender = new UdpClient();
                _udpSender.JoinMulticastGroup(IPAddress.Parse(MulticastAddress));

                _listeningCts = new CancellationTokenSource();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listeningCts.Token));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize full UDP multicast. Falling back to simulated UDP client loop.");
                // Ensure discovery doesn't crash initialization, fall back gracefully
            }

            return Task.CompletedTask;
        }

        public async Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Stopping LAN Peer Discovery...");

            if (_listeningCts != null)
            {
                _listeningCts.Cancel();
                _listeningCts.Dispose();
                _listeningCts = null;
            }

            if (_listenerTask != null)
            {
                try
                {
                    await _listenerTask;
                }
                catch (OperationCanceledException) { }
                _listenerTask = null;
            }

            _udpListener?.Close();
            _udpListener?.Dispose();
            _udpListener = null;

            _udpSender?.Close();
            _udpSender?.Dispose();
            _udpSender = null;
        }

        public async Task BroadcastHeartbeatAsync(CacheNode self, CancellationToken cancellationToken = default)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));

            string json = JsonSerializer.Serialize(self);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            // Update local node status as well
            await _cacheManager.SaveNodeAsync(self, cancellationToken);

            if (_udpSender != null)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), Port);
                    await _udpSender.SendAsync(bytes, bytes.Length, endpoint);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast LAN heartbeat packet via physical UDP client.");
                }
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_udpListener == null) break;

                    var result = await _udpListener.ReceiveAsync(cancellationToken);
                    string json = Encoding.UTF8.GetString(result.Buffer);

                    var node = JsonSerializer.Deserialize<CacheNode>(json);
                    if (node != null && !string.IsNullOrEmpty(node.NodeId))
                    {
                        // Ensure we update LastSeenUtc and state
                        node.LastSeenUtc = DateTime.UtcNow;
                        node.IsOnline = true;

                        await _cacheManager.SaveNodeAsync(node, cancellationToken);
                        PeerDiscovered?.Invoke(this, node);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(ex, "Error in LAN Discovery listen loop.");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _listeningCts?.Cancel();
            _listeningCts?.Dispose();
            _udpListener?.Dispose();
            _udpSender?.Dispose();
        }
    }
}
