using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Repository interface for saving and querying enterprise audit logs and activity metrics.
    /// </summary>
    public interface IAuditMetricRepository
    {
        /// <summary>
        /// Inserts a single audit metric record.
        /// </summary>
        Task InsertAsync(AuditMetric record, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts a batch of audit metric records.
        /// </summary>
        Task BatchInsertAsync(IEnumerable<AuditMetric> records, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries audit metrics based on filtering parameters.
        /// </summary>
        Task<IReadOnlyCollection<AuditMetric>> QueryAsync(
            DateTime? start = null,
            DateTime? end = null,
            string? name = null,
            string? machineId = null,
            string? sessionId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes audit metric records older than the specified cutoff timestamp.
        /// </summary>
        Task DeleteAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);
    }
}
