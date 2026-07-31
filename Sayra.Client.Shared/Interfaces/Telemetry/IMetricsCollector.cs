using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for capturing and recording real-time metric points across subsystems.
    /// </summary>
    public interface IMetricsCollector
    {
        /// <summary>
        /// Asynchronously records a single metric value with optional tag criteria.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="value">The raw numerical value of the metric.</param>
        /// <param name="tags">The tags associated with this metric instance.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RecordMetricAsync(string name, double value, IReadOnlyDictionary<string, string>? tags = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves all metrics captured in the current collection cycle.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A collection of recorded metric points.</returns>
        Task<IReadOnlyCollection<MetricPoint>> GetCollectedMetricsAsync(CancellationToken cancellationToken = default);
    }
}
