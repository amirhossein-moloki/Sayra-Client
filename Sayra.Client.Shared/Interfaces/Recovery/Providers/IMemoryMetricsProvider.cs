using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery.Providers
{
    /// <summary>
    /// Abstraction for gathering system memory (RAM) metrics.
    /// </summary>
    public interface IMemoryMetricsProvider
    {
        /// <summary>
        /// Retrieves the total physical RAM bytes available on the system.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Total physical RAM bytes.</returns>
        Task<long> GetTotalSystemRamBytesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the available free physical RAM bytes on the system.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Available free physical RAM bytes.</returns>
        Task<long> GetAvailableSystemRamBytesAsync(CancellationToken cancellationToken = default);
    }
}
