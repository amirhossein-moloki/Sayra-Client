using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery.Providers
{
    /// <summary>
    /// Abstraction for gathering system CPU metrics.
    /// </summary>
    public interface ICpuMetricsProvider
    {
        /// <summary>
        /// Retrieves the overall system CPU usage percentage (0.0 to 100.0).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>CPU usage percentage.</returns>
        Task<double> GetCpuUsagePercentageAsync(CancellationToken cancellationToken = default);
    }
}
