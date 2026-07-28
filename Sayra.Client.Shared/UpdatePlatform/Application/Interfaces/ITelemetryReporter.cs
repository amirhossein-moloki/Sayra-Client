using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Coordinates non-blocking creation, enrichment, validation, and delivery of telemetry events.
    /// </summary>
    public interface ITelemetryReporter
    {
        /// <summary>
        /// Records an update lifecycle event and processes it asynchronously.
        /// </summary>
        Task RecordEventAsync(
            string eventType,
            string correlationId,
            string sourceVersion,
            string targetVersion,
            bool success,
            string errorCode = "",
            string errorMessage = "",
            string payloadJson = "",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records and uploads general update operation metrics.
        /// </summary>
        Task RecordMetricAsync(UpdateOperationMetric metric, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records and uploads download-specific performance metrics.
        /// </summary>
        Task RecordMetricAsync(DownloadMetric metric, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records and uploads installation-specific metrics.
        /// </summary>
        Task RecordMetricAsync(InstallationMetric metric, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records and uploads rollback-specific metrics.
        /// </summary>
        Task RecordMetricAsync(RollbackMetric metric, CancellationToken cancellationToken = default);

        /// <summary>
        /// Synchronously or asynchronously flushes current in-memory/queued telemetry to the client.
        /// </summary>
        Task FlushAsync(CancellationToken cancellationToken = default);
    }
}
