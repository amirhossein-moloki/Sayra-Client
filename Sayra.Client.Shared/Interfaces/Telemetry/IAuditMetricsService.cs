using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for recording and querying operational audit events and usage metrics.
    /// </summary>
    public interface IAuditMetricsService
    {
        /// <summary>
        /// Asynchronously records a single operational audit metric.
        /// </summary>
        /// <param name="auditMetric">The audit metric record to preserve.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RecordAuditMetricAsync(AuditMetric auditMetric, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously queries historical audit metrics within the specified time frame.
        /// </summary>
        /// <param name="from">The start timestamp filter.</param>
        /// <param name="to">The end timestamp filter.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A collection of matching audit metric records.</returns>
        Task<IReadOnlyCollection<AuditMetric>> GetAuditMetricsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    }
}
