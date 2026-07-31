using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry
{
    /// <summary>
    /// Abstract base class for all workstation telemetry collectors.
    /// Provides failure isolation, timeout protection, execution tracking, and structured logging.
    /// </summary>
    public abstract class BaseTelemetryCollector : IExtendedTelemetryCollector
    {
        protected readonly ILogger Logger;

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public CollectionInterval Interval { get; }

        /// <inheritdoc />
        public int Priority { get; }

        /// <inheritdoc />
        public TimeSpan Timeout { get; }

        /// <inheritdoc />
        public TimeSpan LastExecutionDuration { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// Protected constructor to initialize collector metadata.
        /// </summary>
        protected BaseTelemetryCollector(
            string name,
            CollectionInterval interval,
            int priority,
            TimeSpan? timeout,
            ILogger logger)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Interval = interval;
            Priority = priority;
            Timeout = timeout ?? TimeSpan.FromSeconds(5);
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<TelemetryRecord>> CollectRecordsAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            try
            {
                Logger.LogDebug("Starting collection for collector: {CollectorName}", Name);
                var records = await CollectInternalAsync(cts.Token).ConfigureAwait(false);
                stopwatch.Stop();
                LastExecutionDuration = stopwatch.Elapsed;
                Logger.LogDebug("Completed collection for collector: {CollectorName} in {DurationMs}ms with {Count} records", Name, LastExecutionDuration.TotalMilliseconds, records.Count);
                return records;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                LastExecutionDuration = stopwatch.Elapsed;
                Logger.LogError("Collection timed out for collector: {CollectorName} (Timeout: {TimeoutSeconds}s, Execution Time: {DurationMs}ms)", Name, Timeout.TotalSeconds, LastExecutionDuration.TotalMilliseconds);
                return Array.Empty<TelemetryRecord>();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LastExecutionDuration = stopwatch.Elapsed;
                Logger.LogError(ex, "Failure isolated in collector: {CollectorName} after {DurationMs}ms", Name, LastExecutionDuration.TotalMilliseconds);
                return Array.Empty<TelemetryRecord>();
            }
        }

        /// <inheritdoc />
        public async Task CollectAsync(LiveTelemetryData data, CancellationToken cancellationToken = default)
        {
            if (data == null) return;

            var records = await CollectRecordsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var record in records)
            {
                try
                {
                    MapRecordToLiveData(record, data);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error mapping record {MetricName} to LiveTelemetryData in collector {CollectorName}", record.MetricName, Name);
                }
            }
        }

        /// <summary>
        /// Abstract method subclasses implement to produce telemetry records.
        /// </summary>
        protected abstract Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Maps individual TelemetryRecord values back to the legacy LiveTelemetryData contract.
        /// </summary>
        protected virtual void MapRecordToLiveData(TelemetryRecord record, LiveTelemetryData data)
        {
            // Subclasses can override or handle mapping if they have specific matches.
        }
    }
}
