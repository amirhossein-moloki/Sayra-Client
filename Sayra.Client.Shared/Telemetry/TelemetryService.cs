using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Telemetry
{
    /// <summary>
    /// Production implementation of the ITelemetryService.
    /// Orchestrates scheduled background collection loops, failure isolation,
    /// pipeline execution, and manual telemetry tracking.
    /// </summary>
    public class TelemetryService : ITelemetryService, IDisposable
    {
        private readonly ILogger<TelemetryService> _logger;
        private readonly TelemetryPipeline _pipeline;
        private readonly IOptions<CollectionOptions> _collectionOptions;
        private readonly List<IExtendedTelemetryCollector> _collectors;
        private readonly ConcurrentQueue<TelemetryRecord> _recordsBuffer = new();
        private readonly SemaphoreSlim _loopLock = new(1, 1);

        private CancellationTokenSource? _cts;
        private List<Task>? _collectionTasks;
        private bool _isRunning;
        private bool _disposed;

        public TelemetryService(
            TelemetryPipeline pipeline,
            IEnumerable<IExtendedTelemetryCollector> collectors,
            IOptions<CollectionOptions> collectionOptions,
            ILogger<TelemetryService> logger)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _collectionOptions = collectionOptions ?? throw new ArgumentNullException(nameof(collectionOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _collectors = (collectors ?? throw new ArgumentNullException(nameof(collectors))).ToList();
        }

        /// <inheritdoc />
        public Task TrackMetricAsync(TelemetryRecord record, CancellationToken cancellationToken = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            try
            {
                bool processed = _pipeline.ProcessAndQueue(record);
                if (processed)
                {
                    _recordsBuffer.Enqueue(record);
                    // Prevent memory leaks: Keep buffer at a reasonable size
                    while (_recordsBuffer.Count > 5000)
                    {
                        _recordsBuffer.TryDequeue(out _);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking metric: {MetricName}", record.MetricName);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task StartCollectionAsync(CancellationToken cancellationToken = default)
        {
            await _loopLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_isRunning)
                {
                    _logger.LogWarning("Telemetry collection is already running.");
                    return;
                }

                _logger.LogInformation("Starting workstation telemetry collection platform...");
                _cts = new CancellationTokenSource();
                _collectionTasks = new List<Task>();

                // Spawn a dedicated loop task for each CollectionInterval category
                foreach (CollectionInterval interval in Enum.GetValues(typeof(CollectionInterval)))
                {
                    var intervalCategory = interval;
                    _collectionTasks.Add(Task.Run(() => RunCollectionLoopAsync(intervalCategory, _cts.Token), _cts.Token));
                }

                _isRunning = true;
                _logger.LogInformation("Workstation telemetry collection platform started successfully.");
            }
            finally
            {
                _loopLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task StopCollectionAsync(CancellationToken cancellationToken = default)
        {
            await _loopLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_isRunning)
                {
                    _logger.LogWarning("Telemetry collection is not running.");
                    return;
                }

                _logger.LogInformation("Stopping workstation telemetry collection platform...");
                _cts?.Cancel();

                if (_collectionTasks != null)
                {
                    try
                    {
                        await Task.WhenAll(_collectionTasks).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "One or more collection loops failed during shutdown.");
                    }
                }

                _cts?.Dispose();
                _cts = null;
                _collectionTasks = null;
                _isRunning = false;
                _logger.LogInformation("Workstation telemetry collection platform stopped successfully.");
            }
            finally
            {
                _loopLock.Release();
            }
        }

        private async Task RunCollectionLoopAsync(CollectionInterval interval, CancellationToken token)
        {
            _logger.LogInformation("Collection loop initialized for category: {IntervalCategory}", interval);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Execute collectors of this category
                    await ExecuteCollectorsAsync(interval, token).ConfigureAwait(false);

                    // Dynamic delay reading the options on every cycle
                    int intervalSeconds = GetIntervalSeconds(interval);
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in telemetry collection loop for category {IntervalCategory}", interval);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                    }
                    catch
                    {
                        break;
                    }
                }
            }

            _logger.LogInformation("Collection loop terminated for category: {IntervalCategory}", interval);
        }

        private async Task ExecuteCollectorsAsync(CollectionInterval interval, CancellationToken token)
        {
            // Filter and sort collectors by priority descending
            var targetCollectors = _collectors
                .Where(c => c.Interval == interval)
                .OrderByDescending(c => c.Priority)
                .ToList();

            if (targetCollectors.Count == 0) return;

            _logger.LogDebug("Executing {Count} collectors for category {Category}", targetCollectors.Count, interval);

            foreach (var collector in targetCollectors)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    // Execute collector with failure isolation and timeout protection built into CollectRecordsAsync
                    var records = await collector.CollectRecordsAsync(token).ConfigureAwait(false);
                    foreach (var record in records)
                    {
                        _pipeline.ProcessAndQueue(record);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Isolated failure: Ensure a failing collector never stops the engine or other collectors
                    _logger.LogError(ex, "Failure isolated in scheduler execution for collector: {CollectorName}", collector.Name);
                }
            }
        }

        private int GetIntervalSeconds(CollectionInterval interval)
        {
            var options = _collectionOptions.Value;
            return interval switch
            {
                CollectionInterval.Critical => options.CriticalIntervalSeconds,
                CollectionInterval.Performance => options.PerformanceIntervalSeconds,
                CollectionInterval.Hardware => options.HardwareIntervalSeconds,
                CollectionInterval.Storage => options.StorageIntervalSeconds,
                CollectionInterval.Historical => options.HistoricalIntervalSeconds,
                _ => (int)interval
            };
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _loopLock.Dispose();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
