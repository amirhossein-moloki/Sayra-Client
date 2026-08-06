using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.GameDistribution.Cache.Models;

namespace Sayra.Client.Shared.GameDistribution.Cache.Interfaces
{
    public interface IDistributedCacheManager
    {
        Task SaveGameEntryAsync(GameCacheEntry entry, CancellationToken cancellationToken = default);
        Task<GameCacheEntry?> GetGameEntryAsync(string gameId, CancellationToken cancellationToken = default);
        Task<IEnumerable<GameCacheEntry>> GetAllGameEntriesAsync(CancellationToken cancellationToken = default);
        Task SaveNodeAsync(CacheNode node, CancellationToken cancellationToken = default);
        Task<CacheNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default);
        Task<IEnumerable<CacheNode>> GetOnlineNodesAsync(CancellationToken cancellationToken = default);
        Task SaveBlockAvailabilityAsync(BlockAvailability availability, CancellationToken cancellationToken = default);
        Task<IEnumerable<CacheNode>> GetNodesWithBlockAsync(string blockId, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetAvailableBlocksForNodeAsync(string nodeId, CancellationToken cancellationToken = default);
    }
}
