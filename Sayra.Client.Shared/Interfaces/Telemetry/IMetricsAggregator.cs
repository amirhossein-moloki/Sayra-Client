using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for aggregating raw metric points into structured downsampled series.
    /// </summary>
    public interface IMetricsAggregator
    {
        /// <summary>
        /// Asynchronously aggregates raw collected metric points in the pending buffer.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AggregateMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously gets the aggregated series for a given metric.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The aggregated metric series model.</returns>
        Task<MetricSeries> GetAggregatedSeriesAsync(string name, CancellationToken cancellationToken = default);
    }
}
