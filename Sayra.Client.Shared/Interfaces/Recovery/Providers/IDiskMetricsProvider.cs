using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery.Providers
{
    /// <summary>
    /// Abstraction for gathering system disk/storage metrics.
    /// </summary>
    public interface IDiskMetricsProvider
    {
        /// <summary>
        /// Retrieves the available free disk space in bytes on the drive containing the specified path.
        /// </summary>
        /// <param name="path">The folder or drive path to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Available free disk space bytes.</returns>
        Task<long> GetFreeDiskSpaceBytesAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the overall disk Input/Output transmission rate in bytes per second.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Disk IO bytes per second.</returns>
        Task<double> GetDiskIoBytesPerSecondAsync(CancellationToken cancellationToken = default);
    }
}
