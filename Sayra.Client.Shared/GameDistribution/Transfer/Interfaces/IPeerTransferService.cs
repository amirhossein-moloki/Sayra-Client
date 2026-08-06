using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.GameDistribution.Cache.Models;

namespace Sayra.Client.Shared.GameDistribution.Transfer.Interfaces
{
    public interface IPeerTransferService
    {
        Task StartListenerAsync(string ipAddress, int port, CancellationToken cancellationToken = default);
        Task StopListenerAsync(CancellationToken cancellationToken = default);
        Task<byte[]> TransferBlockAsync(CacheNode sourceNode, string blockId, CancellationToken cancellationToken = default);
        Task<IEnumerable<byte[]>> GetMissingBlocksAsync(string gameId, IEnumerable<string> blockIds, CancellationToken cancellationToken = default);
    }
}
