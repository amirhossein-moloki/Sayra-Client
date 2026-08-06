using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Models;

namespace Sayra.Client.Shared.GameDistribution.BlockStorage.Interfaces
{
    public interface IBlockRepository
    {
        Task SaveAsync(ContentBlock block, CancellationToken cancellationToken = default);
        Task<ContentBlock?> GetAsync(string blockId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ContentBlock>> GetByGameAsync(string gameId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ContentBlock>> GetAllAsync(CancellationToken cancellationToken = default);
        Task DeleteAsync(string blockId, CancellationToken cancellationToken = default);
    }
}
