using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for managing historical downsampled metrics and capacity forecasting.
    /// </summary>
    public interface IHistoricalMetricsService
    {
        /// <summary>
        /// Asynchronously saves a single consolidated historical metric.
        /// </summary>
        /// <param name="metric">The historical metric record to store.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveHistoricalMetricAsync(HistoricalMetric metric, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves historical records matching filters.
        /// </summary>
        /// <param name="name">The name of the metric.</param>
        /// <param name="start">The start timestamp filter.</param>
        /// <param name="end">The end timestamp filter.</param>
        /// <param name="interval">The target downsampled collection interval.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A collection of historical metric records.</returns>
        Task<IReadOnlyCollection<HistoricalMetric>> GetHistoricalMetricsAsync(string name, DateTime start, DateTime end, CollectionInterval interval, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously runs trend analysis and projections to produce capacity forecasts for a metric.
        /// </summary>
        /// <param name="name">The name of the metric to forecast.</param>
        /// <param name="projectionDays">Number of days to forecast into the future.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A capacity forecast model representing future trends.</returns>
        Task<CapacityForecast> ForecastCapacityAsync(string name, int projectionDays, CancellationToken cancellationToken = default);
    }
}
