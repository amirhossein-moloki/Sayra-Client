using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Models;

namespace Sayra.Client.Shared.GameDistribution.Repair.Interfaces
{
    public interface IGameRepairService
    {
        Task<bool> RepairGameAsync(string gameId, IEnumerable<ContentBlock> targetBlocks, CancellationToken cancellationToken = default);
    }
}
