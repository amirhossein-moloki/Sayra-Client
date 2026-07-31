using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for monitoring system runtime performance.
    /// </summary>
    public interface IPerformanceMonitor
    {
        /// <summary>
        /// Asynchronously records a new performance snapshot containing platform latency metrics.
        /// </summary>
        /// <param name="snapshot">The performance snapshot details.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RecordPerformanceSnapshotAsync(PerformanceSnapshot snapshot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves the latest performance snapshot.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The performance snapshot details.</returns>
        Task<PerformanceSnapshot> GetLatestPerformanceSnapshotAsync(CancellationToken cancellationToken = default);
    }
}
