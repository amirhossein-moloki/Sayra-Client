using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Core contract representing a reliable offline buffer for unsent telemetry events.
    /// </summary>
    public interface ITelemetryOfflineQueue
    {
        /// <summary>
        /// Stores an event locally when offline. Enforces size limits and prevents data loss.
        /// </summary>
        Task EnqueueAsync(UpdateTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a batch of pending telemetry events for retry processing.
        /// </summary>
        Task<IEnumerable<UpdateTelemetryEvent>> DequeueBatchAsync(int batchSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes successfully transmitted events from the local database.
        /// </summary>
        Task DeleteBatchAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current number of buffered events.
        /// </summary>
        Task<int> GetCountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Increments the retry attempt count for failing telemetry events.
        /// </summary>
        Task IncrementAttemptCountAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default);
    }
}
