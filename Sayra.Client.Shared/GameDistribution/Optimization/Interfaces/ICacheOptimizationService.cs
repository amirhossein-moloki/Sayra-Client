using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.GameDistribution.Optimization.Interfaces
{
    public interface ICacheOptimizationService
    {
        Task OptimizeCacheAsync(long targetFreeBytes, CancellationToken cancellationToken = default);
    }
}
