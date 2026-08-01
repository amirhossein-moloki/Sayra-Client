using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Extended contract for high-frequency workstation telemetry collectors.
    /// Supports priority execution, cancellation tokens, dynamic timeouts, and duration tracking.
    /// </summary>
    public interface IExtendedTelemetryCollector : ITelemetryCollector
    {
        /// <summary>
        /// Gets the display or diagnostic name of the collector.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the configured collection interval.
        /// </summary>
        CollectionInterval Interval { get; }

        /// <summary>
        /// Gets the execution priority of this collector (higher priority executes first).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets the timeout limit for the collector's execution.
        /// </summary>
        TimeSpan Timeout { get; }

        /// <summary>
        /// Gets the duration of the last collection run.
        /// </summary>
        TimeSpan LastExecutionDuration { get; }

        /// <summary>
        /// Asynchronously executes raw telemetry collection to produce telemetry records.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel collection.</param>
        /// <returns>A collection of harvested TelemetryRecord objects.</returns>
        Task<IReadOnlyCollection<TelemetryRecord>> CollectRecordsAsync(CancellationToken cancellationToken = default);
    }
}
