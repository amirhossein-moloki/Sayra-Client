using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery.Providers
{
    /// <summary>
    /// Abstraction for gathering host-process specific metrics.
    /// </summary>
    public interface IProcessMetricsProvider
    {
        /// <summary>
        /// Retrieves the working set physical memory used by the current process.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Process physical RAM bytes in use.</returns>
        Task<long> GetProcessRamBytesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the total open handle count allocated by the current process.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Open handles count.</returns>
        Task<int> GetHandleCountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the total active thread count in the current process.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Active thread count.</returns>
        Task<int> GetThreadCountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the allocated GDI object count in the current process.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>GDI objects count.</returns>
        Task<int> GetGdiObjectsCountAsync(CancellationToken cancellationToken = default);
    }
}
