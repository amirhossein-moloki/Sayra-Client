using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.GameDistribution.BlockStorage.Interfaces
{
    public interface IContentHasher
    {
        string ComputeHash(byte[] data);
        Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken = default);
    }
}
