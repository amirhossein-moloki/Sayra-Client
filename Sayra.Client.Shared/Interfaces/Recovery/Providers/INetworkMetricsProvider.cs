using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery.Providers
{
    /// <summary>
    /// Abstraction for gathering system network usage metrics.
    /// </summary>
    public interface INetworkMetricsProvider
    {
        /// <summary>
        /// Retrieves the overall network transmission rate (bytes sent and received) in bytes per second.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Network IO bytes per second.</returns>
        Task<double> GetNetworkIoBytesPerSecondAsync(CancellationToken cancellationToken = default);
    }
}
