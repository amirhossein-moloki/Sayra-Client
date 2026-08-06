using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Models;

namespace Sayra.Client.Shared.GameDistribution.BlockStorage.Interfaces
{
    public interface IBlockStorageService
    {
        Task<IEnumerable<ContentBlock>> SplitFileIntoBlocksAsync(string filePath, string gameId, string packageId, string version, long blockSize = 1024 * 1024, CancellationToken cancellationToken = default);
        Task SaveBlockBytesAsync(string blockId, byte[] data, CancellationToken cancellationToken = default);
        Task<byte[]> GetBlockBytesAsync(string blockId, CancellationToken cancellationToken = default);
        Task<bool> VerifyBlockAsync(string blockId, CancellationToken cancellationToken = default);
        Task DeleteBlockAsync(string blockId, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> QueryMissingBlocksAsync(string gameId, IEnumerable<string> requiredBlockIds, CancellationToken cancellationToken = default);
    }
}
