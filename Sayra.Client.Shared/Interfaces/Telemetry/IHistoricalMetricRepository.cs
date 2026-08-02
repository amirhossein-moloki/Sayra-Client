using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Repository interface for long-term downsampled historical metrics.
    /// </summary>
    public interface IHistoricalMetricRepository
    {
        /// <summary>
        /// Inserts a single historical metric.
        /// </summary>
        Task InsertAsync(HistoricalMetric metric, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts a batch of historical metrics in a single transactional step.
        /// </summary>
        Task BatchInsertAsync(IEnumerable<HistoricalMetric> metrics, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries historical metrics based on specific parameters.
        /// </summary>
        Task<IReadOnlyCollection<HistoricalMetric>> QueryAsync(
            string? name = null,
            DateTime? start = null,
            DateTime? end = null,
            CollectionInterval? interval = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Prunes historical metrics older than the specified timestamp.
        /// </summary>
        Task DeleteAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves metrics that are eligible for pruning/archiving.
        /// </summary>
        Task<IReadOnlyCollection<HistoricalMetric>> GetExpiredAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);
    }
}
