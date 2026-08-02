using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Repository interface for saving and querying raw time-series metric series.
    /// </summary>
    public interface IMetricSeriesRepository
    {
        /// <summary>
        /// Saves or appends data points into a specific MetricSeries.
        /// </summary>
        Task SaveSeriesAsync(MetricSeries series, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the entire time-series for a given metric.
        /// </summary>
        Task<MetricSeries?> GetSeriesAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the time-series points within a specific date range.
        /// </summary>
        Task<MetricSeries?> QuerySeriesAsync(string name, DateTime start, DateTime end, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cleans up metrics series data points older than the cutoff timestamp.
        /// </summary>
        Task DeleteAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);
    }
}
