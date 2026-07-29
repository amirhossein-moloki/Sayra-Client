using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for monitoring workstation system resources and applying backpressure or graceful degradation rules.
    /// </summary>
    public interface IResourceMonitor
    {
        /// <summary>
        /// Audits resource consumption (CPU, RAM, Disk, Handles, Threads) asynchronously and triggers mitigation policies if low thresholds are crossed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the audit operation.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task RunResourceAuditAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the current workstation resource metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the current ResourceMetrics snapshot.</returns>
        Task<ResourceMetrics> GetResourceMetricsAsync(CancellationToken cancellationToken = default);
    }
}
