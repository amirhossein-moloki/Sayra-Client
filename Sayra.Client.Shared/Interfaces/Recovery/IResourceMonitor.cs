using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for monitoring workstation system resources and tracking resource pressure.
    /// </summary>
    public interface IResourceMonitor
    {
        /// <summary>
        /// Audits resource consumption (CPU, RAM, Disk, Handles, Threads) asynchronously and triggers mitigation policies if low thresholds are crossed.
        /// Preserved for backward compatibility.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the audit operation.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task RunResourceAuditAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the current workstation resource metrics.
        /// Preserved for backward compatibility.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the current ResourceMetrics snapshot.</returns>
        Task<ResourceMetrics> GetResourceMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves the current resource metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the current ResourceMetrics.</returns>
        Task<ResourceMetrics> GetCurrentMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a snapshot of the workstation resource health.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning a snapshot of the ResourceMetrics.</returns>
        Task<ResourceMetrics> GetResourceSnapshotAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a single/continuous resource monitoring pass or sampling.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task MonitorAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to resource monitoring events asynchronously.
        /// </summary>
        /// <param name="handler">The handler invoked when resource events are raised.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous subscription operation.</returns>
        Task SubscribeToResourceEvents(Action<object> handler, CancellationToken cancellationToken = default);
    }
}
