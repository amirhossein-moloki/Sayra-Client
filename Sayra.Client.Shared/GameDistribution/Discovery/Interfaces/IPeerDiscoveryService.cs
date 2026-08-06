using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.GameDistribution.Cache.Models;

namespace Sayra.Client.Shared.GameDistribution.Discovery.Interfaces
{
    public interface IPeerDiscoveryService : IDisposable
    {
        event EventHandler<CacheNode>? PeerDiscovered;
        Task StartDiscoveryAsync(CancellationToken cancellationToken = default);
        Task StopDiscoveryAsync(CancellationToken cancellationToken = default);
        Task BroadcastHeartbeatAsync(CacheNode self, CancellationToken cancellationToken = default);
    }
}
