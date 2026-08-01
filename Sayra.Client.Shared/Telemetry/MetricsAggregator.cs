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
using Sayra.Client.Shared.Models.Telemetry.Exceptions;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Telemetry.Metrics;

namespace Sayra.Client.Shared.Telemetry
{
    /// <summary>
    /// Production implementation of the IMetricsAggregator.
    /// Manages windowing, statistical aggregations, downsampling, and raw validation of telemetry samples.
    /// </summary>
    public class MetricsAggregator : IMetricsAggregator
    {
        private readonly ILogger<MetricsAggregator> _logger;
        private readonly TelemetryPipeline _pipeline;
        private readonly IOptions<MetricsOptions> _metricsOptions;
        private readonly MetricValidator _validator;
        private readonly Dictionary<AggregationType, IMetricAggregatorStrategy> _strategies;

        // In-memory raw telemetry records waiting to be aggregated, grouped by metric name
        private readonly ConcurrentDictionary<string, List<TelemetryRecord>> _rawBuffer = new(StringComparer.OrdinalIgnoreCase);

        // Aggregated series store: Key is (MetricName, WindowSeconds) -> List of aggregated MetricPoint
        private readonly ConcurrentDictionary<(string MetricName, int WindowSeconds), List<MetricPoint>> _aggregatedStore = new();
        private readonly object _storeLock = new();

        public MetricsAggregator(
            TelemetryPipeline pipeline,
            IOptions<MetricsOptions> metricsOptions,
            ILogger<MetricsAggregator> logger,
            IEnumerable<IMetricAggregatorStrategy>? strategies = null)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _metricsOptions = metricsOptions ?? throw new ArgumentNullException(nameof(metricsOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validator = new MetricValidator(logger);

            // Populate strategies with pluggable ones or fallback defaults
            _strategies = new Dictionary<AggregationType, IMetricAggregatorStrategy>();
            var injectedStrategies = strategies?.ToList() ?? new List<IMetricAggregatorStrategy>();

            var defaultStrategies = new List<IMetricAggregatorStrategy>
            {
                new CounterAggregatorStrategy(),
                new GaugeAggregatorStrategy(),
                new HistogramAggregatorStrategy(),
                new TimerAggregatorStrategy(),
                new RateAggregatorStrategy()
            };

            foreach (var strategy in defaultStrategies)
            {
                _strategies[strategy.Type] = strategy;
            }

            foreach (var strategy in injectedStrategies)
            {
                _strategies[strategy.Type] = strategy;
            }
        }

        /// <inheritdoc />
        public Task AggregateMetricsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 1. Drain raw records from the TelemetryPipeline
                int drainedCount = DrainTelemetryPipeline();
                if (drainedCount > 0)
                {
                    _logger.LogDebug("Drained {Count} records from TelemetryPipeline for aggregation.", drainedCount);
                }

                // 2. Perform aggregation across all configured windows
                var options = _metricsOptions.Value;
                var windows = options.ConfiguredWindowsSeconds ?? new List<int> { 5, 15, 30, 60, 300, 900, 3600 };

                foreach (var metricKvp in _rawBuffer)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string metricName = metricKvp.Key;
                    List<TelemetryRecord> records;

                    lock (metricKvp.Value)
                    {
                        if (metricKvp.Value.Count == 0) continue;
                        records = new List<TelemetryRecord>(metricKvp.Value);
                        metricKvp.Value.Clear(); // Clear the buffer since we are aggregating them
                    }

                    // Clean and validate batch, checking for duplicate samples
                    var cleanRecords = _validator.FilterAndCleanBatch(records);
                    if (cleanRecords.Count == 0) continue;

                    // Determine aggregation type and category/unit from the first record
                    var sampleRecord = cleanRecords[0];
                    var aggType = DetermineAggregationType(metricName);

                    if (!_strategies.TryGetValue(aggType, out var strategy))
                    {
                        _logger.LogWarning("No aggregator strategy found for type {Type}. Falling back to Gauge.", aggType);
                        strategy = _strategies[AggregationType.Gauge];
                    }

                    // Group by each configured window duration
                    foreach (int windowSeconds in windows)
                    {
                        var groupedByWindow = GroupRecordsByWindow(cleanRecords, windowSeconds);

                        foreach (var windowGroup in groupedByWindow)
                        {
                            DateTime windowStart = windowGroup.Key;
                            DateTime windowEnd = windowStart.AddSeconds(windowSeconds);

                            // Run pluggable strategy to compute the aggregated MetricPoint
                            var aggregatedPoint = strategy.Aggregate(metricName, windowGroup.Value, windowStart, windowEnd);

                            // Apply moving averages if enabled
                            if (options.EnableMovingAverages)
                            {
                                aggregatedPoint = ApplyMovingAverages(metricName, windowSeconds, aggregatedPoint);
                            }

                            // Downsample if required by comparing with the default window
                            if (windowSeconds > options.AggregationWindowSeconds)
                            {
                                aggregatedPoint = ApplyDownsampling(metricName, options.DownsamplingStrategy, aggregatedPoint);
                            }

                            // Store in-memory
                            var storeKey = (metricName.ToLowerInvariant(), windowSeconds);
                            lock (_storeLock)
                            {
                                if (!_aggregatedStore.TryGetValue(storeKey, out var pointList))
                                {
                                    pointList = new List<MetricPoint>();
                                    _aggregatedStore[storeKey] = pointList;
                                }

                                pointList.Add(aggregatedPoint);

                                // Limit history size per series to prevent memory leaks (e.g., keep last 1000 points)
                                while (pointList.Count > 1000)
                                {
                                    pointList.RemoveAt(0);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing metrics aggregation cycle.");
                throw new MetricsException("Failed to aggregate metrics.", ex);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<MetricSeries> GetAggregatedSeriesAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));
            }

            int defaultWindow = _metricsOptions.Value.AggregationWindowSeconds;
            var storeKey = (name.Trim().ToLowerInvariant(), defaultWindow);

            lock (_storeLock)
            {
                if (_aggregatedStore.TryGetValue(storeKey, out var points))
                {
                    var series = new MetricSeries
                    {
                        MetricName = name,
                        Category = DetermineCategory(name),
                        Unit = DetermineUnit(name),
                        Points = points.ToList()
                    };
                    return Task.FromResult(series);
                }
            }

            // Return empty series if not found
            return Task.FromResult(new MetricSeries
            {
                MetricName = name,
                Category = DetermineCategory(name),
                Unit = DetermineUnit(name),
                Points = Array.Empty<MetricPoint>()
            });
        }

        private int DrainTelemetryPipeline()
        {
            int count = 0;
            // Drain the pipeline's channel reader non-blockingly
            while (_pipeline.Reader.TryRead(out var record))
            {
                if (record == null) continue;

                var bufferList = _rawBuffer.GetOrAdd(record.MetricName, _ => new List<TelemetryRecord>());
                lock (bufferList)
                {
                    bufferList.Add(record);
                    // Protect against buffer overflow
                    while (bufferList.Count > 10000)
                    {
                        bufferList.RemoveAt(0);
                    }
                }
                count++;
            }
            return count;
        }

        private Dictionary<DateTime, List<TelemetryRecord>> GroupRecordsByWindow(IReadOnlyList<TelemetryRecord> records, int windowSeconds)
        {
            var groups = new Dictionary<DateTime, List<TelemetryRecord>>();

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                long unixSeconds = ((DateTimeOffset)record.Timestamp.ToUniversalTime()).ToUnixTimeSeconds();
                long bucketUnix = (unixSeconds / windowSeconds) * windowSeconds;
                DateTime bucketStart = DateTimeOffset.FromUnixTimeSeconds(bucketUnix).UtcDateTime;

                if (!groups.TryGetValue(bucketStart, out var list))
                {
                    list = new List<TelemetryRecord>();
                    groups[bucketStart] = list;
                }
                list.Add(record);
            }

            return groups;
        }

        private MetricPoint ApplyMovingAverages(string metricName, int windowSeconds, MetricPoint newPoint)
        {
            var storeKey = (metricName.ToLowerInvariant(), windowSeconds);
            List<MetricPoint> existingPoints;

            lock (_storeLock)
            {
                if (!_aggregatedStore.TryGetValue(storeKey, out var list) || list.Count == 0)
                {
                    return newPoint;
                }
                existingPoints = list.ToList();
            }

            var allValues = existingPoints.Select(p => p.Value).Concat(new[] { newPoint.Value }).ToList();

            // Calculate rolling simple moving average (SMA) of last 10 points
            var rollingAverages = MetricsMath.CalculateRollingAverages(allValues, 10);
            double currentRollingAvg = rollingAverages[^1];

            // Calculate exponential moving average (EMA) with standard alpha = 0.2
            var exponentialAverages = MetricsMath.CalculateExponentialMovingAverages(allValues, 0.2);
            double currentEma = exponentialAverages[^1];

            var enrichedTags = new Dictionary<string, string>(newPoint.Tags ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
            {
                ["rolling_average"] = currentRollingAvg.ToString("F2"),
                ["moving_average_ema"] = currentEma.ToString("F2")
            };

            return newPoint with { Tags = enrichedTags };
        }

        private MetricPoint ApplyDownsampling(string metricName, string downsampleStrategy, MetricPoint originalPoint)
        {
            // Apply downsampling strategy transformation as configured
            var pointsList = new List<MetricPoint> { originalPoint };
            return MetricDownsampler.Downsample(pointsList, downsampleStrategy, originalPoint.Timestamp);
        }

        private AggregationType DetermineAggregationType(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains(".latency") || lower.Contains(".ms") || lower.Contains(".duration") || lower.Contains(".time") || lower.Contains(".ping"))
            {
                return AggregationType.Timer;
            }
            if (lower.Contains(".speed") || lower.Contains(".rate") || lower.Contains(".throughput"))
            {
                return AggregationType.Rate;
            }
            if (lower.Contains(".count") || lower.Contains(".total") || lower.Contains(".num") || lower.Contains(".sum"))
            {
                return AggregationType.Counter;
            }
            if (lower.Contains(".distribution") || lower.Contains(".histogram") || lower.Contains(".range"))
            {
                return AggregationType.Histogram;
            }
            return AggregationType.Gauge;
        }

        private MetricCategory DetermineCategory(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains(".cpu")) return MetricCategory.Cpu;
            if (lower.Contains(".memory") || lower.Contains(".ram")) return MetricCategory.Memory;
            if (lower.Contains(".gpu") || lower.Contains(".vram") || lower.Contains(".fps")) return MetricCategory.Gpu;
            if (lower.Contains(".disk") || lower.Contains(".storage")) return MetricCategory.Disk;
            if (lower.Contains(".network") || lower.Contains(".ping")) return MetricCategory.Network;
            if (lower.Contains(".database") || lower.Contains(".sql")) return MetricCategory.Database;
            if (lower.Contains(".ipc") || lower.Contains(".pipe")) return MetricCategory.Ipc;
            if (lower.Contains(".sync")) return MetricCategory.Sync;
            if (lower.Contains(".notification")) return MetricCategory.Notification;
            if (lower.Contains(".overlay")) return MetricCategory.Overlay;
            if (lower.Contains(".watchdog")) return MetricCategory.Watchdog;
            if (lower.Contains(".policy")) return MetricCategory.Policy;
            if (lower.Contains(".plugin")) return MetricCategory.Plugin;
            if (lower.Contains(".download")) return MetricCategory.Download;
            if (lower.Contains(".update")) return MetricCategory.Update;
            if (lower.Contains(".game")) return MetricCategory.Game;
            if (lower.Contains(".session")) return MetricCategory.Session;

            return MetricCategory.Process;
        }

        private MetricUnit DetermineUnit(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains(".usage") || lower.Contains(".percent")) return MetricUnit.Percent;
            if (lower.Contains(".ms") || lower.Contains(".latency") || lower.Contains(".ping")) return MetricUnit.Milliseconds;
            if (lower.Contains(".duration") || lower.Contains(".seconds")) return MetricUnit.Seconds;
            if (lower.Contains(".bytes_") || lower.Contains(".speed")) return MetricUnit.Bytes;
            if (lower.Contains(".mb") || lower.Contains(".vram")) return MetricUnit.Megabytes;
            if (lower.Contains(".gb") || lower.Contains(".space")) return MetricUnit.Gigabytes;
            if (lower.Contains(".rate") || lower.Contains(".fps")) return MetricUnit.Rate;

            return MetricUnit.Count;
        }
    }
}
