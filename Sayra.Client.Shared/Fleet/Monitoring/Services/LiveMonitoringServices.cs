using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Fleet.Monitoring.Domain.Events;
using Sayra.Client.Shared.Fleet.Monitoring.Domain.Models;
using Sayra.Client.Shared.Fleet.Monitoring.Interfaces;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Events;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Options;

namespace Sayra.Client.Shared.Fleet.Monitoring.Services
{
    /// <summary>
    /// Thread-safe in-memory cache manager storing expiring telemetry, snapshots, health, and historical series.
    /// </summary>
    public class MonitoringCache : IMonitoringCache
    {
        private readonly ConcurrentDictionary<string, LiveMonitoringSnapshot> _latestSnapshots = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<LiveMonitoringSnapshot>> _historicalSnapshots = new();
        private readonly IOptions<MonitoringOptions> _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoringCache"/> class.
        /// </summary>
        public MonitoringCache(IOptions<MonitoringOptions> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public void SetSnapshot(string machineId, LiveMonitoringSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            if (snapshot == null) return;

            _latestSnapshots[machineId] = snapshot;

            var queue = _historicalSnapshots.GetOrAdd(machineId, _ => new ConcurrentQueue<LiveMonitoringSnapshot>());
            queue.Enqueue(snapshot);

            // Limit buffer size to prevent memory exhaustion
            int maxHistory = _options.Value.TelemetryBufferSize > 0 ? _options.Value.TelemetryBufferSize : 200;
            while (queue.Count > maxHistory)
            {
                queue.TryDequeue(out _);
            }
        }

        /// <inheritdoc />
        public LiveMonitoringSnapshot? GetSnapshot(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return null;

            if (_latestSnapshots.TryGetValue(machineId, out var snapshot))
            {
                // Check for soft expiration
                if (DateTime.UtcNow > snapshot.ExpiresAtUtc)
                {
                    return null; // Expired
                }
                return snapshot;
            }
            return null;
        }

        /// <inheritdoc />
        public IReadOnlyList<LiveMonitoringSnapshot> GetAllSnapshots()
        {
            return _latestSnapshots.Values.ToList();
        }

        /// <inheritdoc />
        public IReadOnlyList<LiveMonitoringSnapshot> GetHistory(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return Array.Empty<LiveMonitoringSnapshot>();

            if (_historicalSnapshots.TryGetValue(machineId, out var queue))
            {
                return queue.ToList();
            }
            return Array.Empty<LiveMonitoringSnapshot>();
        }

        /// <inheritdoc />
        public void Invalidate(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            _latestSnapshots.TryRemove(machineId, out _);
            _historicalSnapshots.TryRemove(machineId, out _);
        }

        /// <inheritdoc />
        public void OptimizeMemoryUsage()
        {
            // Evict expired entries globally
            var now = DateTime.UtcNow;
            foreach (var kvp in _latestSnapshots.ToList())
            {
                if (now > kvp.Value.ExpiresAtUtc)
                {
                    _latestSnapshots.TryRemove(kvp.Key, out _);
                    _historicalSnapshots.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    /// <summary>
    /// Engine adjusting sampling frequency based on adaptive load or burst-triggered intervals.
    /// </summary>
    public class SamplingEngine : ISamplingEngine
    {
        private readonly IOptions<MonitoringOptions> _options;
        private readonly ConcurrentDictionary<string, DateTime> _burstEndTimeUtc = new();
        private readonly ConcurrentDictionary<string, bool> _highLoadWorkstations = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="SamplingEngine"/> class.
        /// </summary>
        public SamplingEngine(IOptions<MonitoringOptions> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public int GetSamplingIntervalMs(string machineId)
        {
            int baseInterval = _options.Value.SamplingIntervalMs > 0 ? _options.Value.SamplingIntervalMs : 1000;

            // Check if active burst sampling window is running (high priority)
            if (_burstEndTimeUtc.TryGetValue(machineId, out var burstEnd) && DateTime.UtcNow < burstEnd)
            {
                return 100; // High-frequency polling rate under burst request
            }

            // Check if adaptive sampling applies
            if (_highLoadWorkstations.TryGetValue(machineId, out var highLoad) && highLoad)
            {
                return Math.Max(200, baseInterval / 2); // Speed up collection when load is abnormal
            }

            return baseInterval;
        }

        /// <inheritdoc />
        public void TriggerBurstSampling(string machineId, TimeSpan duration)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            _burstEndTimeUtc[machineId] = DateTime.UtcNow.Add(duration);
        }

        /// <inheritdoc />
        public void UpdateLoadState(string machineId, bool isHighLoad)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            _highLoadWorkstations[machineId] = isHighLoad;
        }
    }

    /// <summary>
    /// Coordinator invoking all registered metric collectors concurrently to compile a raw snapshot.
    /// </summary>
    public class PollingEngine : IPollingEngine
    {
        private readonly IEnumerable<ILiveMetricCollector> _collectors;
        private readonly ILogger<PollingEngine> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PollingEngine"/> class.
        /// </summary>
        public PollingEngine(IEnumerable<ILiveMetricCollector> collectors, ILogger<PollingEngine> logger)
        {
            _collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<LiveMonitoringSnapshot> PollMetricsAsync(string machineId, CancellationToken ct = default)
        {
            var builder = new LiveMonitoringSnapshotBuilder
            {
                MachineId = machineId,
                TimestampUtc = DateTime.UtcNow
            };

            var tasks = _collectors.Select(async collector =>
            {
                try
                {
                    await collector.CollectAsync(builder, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Collector {CollectorName} failed during poll for machine {MachineId}.", collector.MetricName, machineId);
                }
            });

            await Task.WhenAll(tasks);
            return builder.Build();
        }
    }

    /// <summary>
    /// Service managing creation, comparison, historical series, and compression of workstation snapshots.
    /// </summary>
    public class SnapshotEngine : ISnapshotEngine
    {
        /// <inheritdoc />
        public LiveMonitoringDeltaSnapshot ComputeDelta(LiveMonitoringSnapshot current, LiveMonitoringSnapshot previous)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (previous == null) throw new ArgumentNullException(nameof(previous));

            return new LiveMonitoringDeltaSnapshot
            {
                CpuUsageDelta = Math.Round(current.CpuUsage - previous.CpuUsage, 2),
                MemoryPressureDelta = Math.Round(current.MemoryPressurePercentage - previous.MemoryPressurePercentage, 2),
                DiskActivityDelta = Math.Round(current.DiskActivityPercentage - previous.DiskActivityPercentage, 2),
                NetworkThroughputDelta = Math.Round((current.NetworkDownloadBytesPerSec + current.NetworkUploadBytesPerSec) -
                                                   (previous.NetworkDownloadBytesPerSec + previous.NetworkUploadBytesPerSec), 2),
                StatusChanged = current.MachineStatus != previous.MachineStatus,
                PreviousMachineStatus = previous.MachineStatus,
                NewMachineStatus = current.MachineStatus,
                HealthChanged = current.OverallHealth != previous.OverallHealth,
                PreviousHealth = previous.OverallHealth,
                NewHealth = current.OverallHealth
            };
        }

        /// <inheritdoc />
        public LiveMonitoringSnapshot CompileAggregate(string machineId, IEnumerable<LiveMonitoringSnapshot> history)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentException("Machine ID cannot be empty", nameof(machineId));
            if (history == null || !history.Any()) throw new ArgumentException("History sequence cannot be empty", nameof(history));

            var list = history.ToList();
            var latest = list.Last();

            return latest with
            {
                SnapshotId = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                CpuUsage = Math.Round(list.Average(s => s.CpuUsage), 2),
                CpuFrequencyGhz = Math.Round(list.Average(s => s.CpuFrequencyGhz), 2),
                CpuLoad = Math.Round(list.Average(s => s.CpuLoad), 2),
                MemoryUsageBytes = Math.Round(list.Average(s => s.MemoryUsageBytes)),
                MemoryPressurePercentage = Math.Round(list.Average(s => s.MemoryPressurePercentage), 2),
                DiskUsageBytes = Math.Round(list.Average(s => s.DiskUsageBytes)),
                DiskFreeSpaceBytes = Math.Round(list.Average(s => s.DiskFreeSpaceBytes)),
                DiskActivityPercentage = Math.Round(list.Average(s => s.DiskActivityPercentage), 2),
                GpuUsage = Math.Round(list.Average(s => s.GpuUsage), 2),
                GpuMemoryUsageBytes = Math.Round(list.Average(s => s.GpuMemoryUsageBytes)),
                GpuTemperatureCelsius = Math.Round(list.Average(s => s.GpuTemperatureCelsius), 1),
                CpuTemperatureCelsius = Math.Round(list.Average(s => s.CpuTemperatureCelsius), 1),
                MotherboardTemperatureCelsius = Math.Round(list.Average(s => s.MotherboardTemperatureCelsius), 1),
                NetworkUploadBytesPerSec = Math.Round(list.Average(s => s.NetworkUploadBytesPerSec), 2),
                NetworkDownloadBytesPerSec = Math.Round(list.Average(s => s.NetworkDownloadBytesPerSec), 2),
                NetworkUtilizationPercentage = Math.Round(list.Average(s => s.NetworkUtilizationPercentage), 2),
                LatencyMs = Math.Round(list.Average(s => s.LatencyMs), 2),
                PacketLossPercentage = Math.Round(list.Average(s => s.PacketLossPercentage), 2),
                JitterMs = Math.Round(list.Average(s => s.JitterMs), 2)
            };
        }

        /// <inheritdoc />
        public byte[] CompressSnapshot(LiveMonitoringSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            string json = JsonSerializer.Serialize(snapshot);
            byte[] rawBytes = Encoding.UTF8.GetBytes(json);

            using var outputStream = new MemoryStream();
            using (var gzip = new GZipStream(outputStream, CompressionMode.Compress, true))
            {
                gzip.Write(rawBytes, 0, rawBytes.Length);
            }
            return outputStream.ToArray();
        }

        /// <inheritdoc />
        public LiveMonitoringSnapshot DecompressSnapshot(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0) throw new ArgumentException("Data to decompress cannot be empty", nameof(compressedData));

            using var inputStream = new MemoryStream(compressedData);
            using var gzip = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();
            gzip.CopyTo(outputStream);

            string json = Encoding.UTF8.GetString(outputStream.ToArray());
            return JsonSerializer.Deserialize<LiveMonitoringSnapshot>(json) ?? throw new InvalidOperationException("Failed to deserialize decompressed snapshot");
        }
    }

    /// <summary>
    /// Engine calculating averages, moving averages, standard percentiles, and trend/change detections.
    /// </summary>
    public class AggregationEngine : IAggregationEngine
    {
        /// <inheritdoc />
        public double ComputeMovingAverage(IEnumerable<double> values)
        {
            if (values == null || !values.Any()) return 0;
            return Math.Round(values.Average(), 2);
        }

        /// <inheritdoc />
        public double ComputePercentile(IEnumerable<double> values, double percentile)
        {
            if (values == null || !values.Any()) return 0;
            var list = values.OrderBy(v => v).ToList();
            if (list.Count == 1) return list[0];

            double realIndex = (list.Count - 1) * (percentile / 100.0);
            int index = (int)realIndex;
            double fraction = realIndex - index;

            if (index + 1 < list.Count)
            {
                return Math.Round(list[index] + (fraction * (list[index + 1] - list[index])), 2);
            }
            return Math.Round(list[index], 2);
        }

        /// <inheritdoc />
        public string DetectTrend(IEnumerable<double> values)
        {
            if (values == null || !values.Any()) return "Stable";
            var list = values.ToList();
            if (list.Count < 3) return "Stable";

            // Simple linear slope detection
            double sumX = 0;
            double sumY = 0;
            double sumX2 = 0;
            double sumXY = 0;
            int n = list.Count;

            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += list[i];
                sumX2 += i * i;
                sumXY += i * list[i];
            }

            double denominator = (n * sumX2) - (sumX * sumX);
            if (Math.Abs(denominator) < 0.0001) return "Stable";

            double slope = ((n * sumXY) - (sumX * sumY)) / denominator;

            if (slope > 0.5) return "Increasing";
            if (slope < -0.5) return "Decreasing";
            return "Stable";
        }

        /// <inheritdoc />
        public bool DetectPeak(IEnumerable<double> values, double currentValue, double thresholdStandardDeviations)
        {
            if (values == null || !values.Any()) return false;
            var list = values.ToList();
            if (list.Count < 3) return false;

            double avg = list.Average();
            double variance = list.Sum(v => Math.Pow(v - avg, 2)) / list.Count;
            double stdDev = Math.Sqrt(variance);

            if (stdDev < 0.001) return false;

            return currentValue > (avg + (thresholdStandardDeviations * stdDev));
        }
    }

    /// <summary>
    /// Evaluator matching live metrics against Warning, Critical, and Emergency thresholds.
    /// </summary>
    public class ThresholdEvaluator : IThresholdEvaluator
    {
        private readonly ConcurrentDictionary<string, ThresholdConfig> _thresholds = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ThresholdEvaluator"/> class with default limits.
        /// </summary>
        public ThresholdEvaluator()
        {
            // Register standard metric thresholds
            ConfigureThreshold("CPU", new ThresholdConfig { WarningLimit = 80, CriticalLimit = 90, EmergencyLimit = 95 });
            ConfigureThreshold("Memory", new ThresholdConfig { WarningLimit = 85, CriticalLimit = 92, EmergencyLimit = 97 });
            ConfigureThreshold("Disk", new ThresholdConfig { WarningLimit = 85, CriticalLimit = 90, EmergencyLimit = 95 });
            ConfigureThreshold("GPU", new ThresholdConfig { WarningLimit = 80, CriticalLimit = 90, EmergencyLimit = 95 });
            ConfigureThreshold("Temperature", new ThresholdConfig { WarningLimit = 75, CriticalLimit = 85, EmergencyLimit = 90 });
            ConfigureThreshold("Latency", new ThresholdConfig { WarningLimit = 150, CriticalLimit = 300, EmergencyLimit = 500 });
            ConfigureThreshold("PacketLoss", new ThresholdConfig { WarningLimit = 2, CriticalLimit = 5, EmergencyLimit = 10 });
        }

        /// <inheritdoc />
        public void ConfigureThreshold(string metricName, ThresholdConfig config)
        {
            if (string.IsNullOrEmpty(metricName)) return;
            _thresholds[metricName] = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <inheritdoc />
        public MachineHealthStatus Evaluate(string machineId, string metricName, double value, out double limitValue)
        {
            limitValue = 0;
            if (!_thresholds.TryGetValue(metricName, out var config))
            {
                return MachineHealthStatus.Healthy;
            }

            if (config.ViolateOnHigher)
            {
                if (value >= config.EmergencyLimit)
                {
                    limitValue = config.EmergencyLimit;
                    return MachineHealthStatus.Emergency;
                }
                if (value >= config.CriticalLimit)
                {
                    limitValue = config.CriticalLimit;
                    return MachineHealthStatus.Critical;
                }
                if (value >= config.WarningLimit)
                {
                    limitValue = config.WarningLimit;
                    return MachineHealthStatus.Warning;
                }
            }
            else
            {
                if (value <= config.EmergencyLimit)
                {
                    limitValue = config.EmergencyLimit;
                    return MachineHealthStatus.Emergency;
                }
                if (value <= config.CriticalLimit)
                {
                    limitValue = config.CriticalLimit;
                    return MachineHealthStatus.Critical;
                }
                if (value <= config.WarningLimit)
                {
                    limitValue = config.WarningLimit;
                    return MachineHealthStatus.Warning;
                }
            }

            return MachineHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Pipeline orchestrating the processing, validation, tag enrichment, and threshold evaluations of snapshots.
    /// </summary>
    public class MonitoringPipeline : IMonitoringPipeline
    {
        private readonly IThresholdEvaluator _thresholdEvaluator;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MachineHealthStatus>> _activeViolations = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoringPipeline"/> class.
        /// </summary>
        public MonitoringPipeline(IThresholdEvaluator thresholdEvaluator, IEventDispatcher eventDispatcher)
        {
            _thresholdEvaluator = thresholdEvaluator ?? throw new ArgumentNullException(nameof(thresholdEvaluator));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        }

        /// <inheritdoc />
        public Task<LiveMonitoringSnapshot> ProcessSnapshotAsync(LiveMonitoringSnapshot snapshot, CancellationToken ct = default)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            double score = 100.0;
            var machineViolations = _activeViolations.GetOrAdd(snapshot.MachineId, _ => new ConcurrentDictionary<string, MachineHealthStatus>());

            // Evaluate CPU Usage
            score -= EvaluateAndAlert(snapshot.MachineId, "CPU", snapshot.CpuUsage, machineViolations);

            // Evaluate Memory Pressure
            score -= EvaluateAndAlert(snapshot.MachineId, "Memory", snapshot.MemoryPressurePercentage, machineViolations);

            // Evaluate GPU Usage
            score -= EvaluateAndAlert(snapshot.MachineId, "GPU", snapshot.GpuUsage, machineViolations);

            // Evaluate Latency
            score -= EvaluateAndAlert(snapshot.MachineId, "Latency", snapshot.LatencyMs, machineViolations);

            // Evaluate Packet Loss
            score -= EvaluateAndAlert(snapshot.MachineId, "PacketLoss", snapshot.PacketLossPercentage, machineViolations);

            // Evaluate CPU Temperature
            score -= EvaluateAndAlert(snapshot.MachineId, "Temperature", snapshot.CpuTemperatureCelsius, machineViolations);

            score = Math.Max(0.0, score);

            // Determine Overall Health Level
            MachineHealthStatus healthTier = MachineHealthStatus.Healthy;
            if (score < 40.0) healthTier = MachineHealthStatus.Emergency;
            else if (score < 70.0) healthTier = MachineHealthStatus.Critical;
            else if (score < 90.0) healthTier = MachineHealthStatus.Warning;

            var finalSnapshot = snapshot with
            {
                OverallHealthScore = score,
                OverallHealth = healthTier
            };

            return Task.FromResult(finalSnapshot);
        }

        private double EvaluateAndAlert(string machineId, string metricName, double value, ConcurrentDictionary<string, MachineHealthStatus> violations)
        {
            var status = _thresholdEvaluator.Evaluate(machineId, metricName, value, out double limit);
            violations.TryGetValue(metricName, out var prevStatus);

            if (status != prevStatus)
            {
                if (status != MachineHealthStatus.Healthy)
                {
                    // Trigger threshold exceeded event
                    _eventDispatcher.Dispatch(new MetricThresholdExceeded(machineId, metricName, value, limit, status));
                }
                else if (prevStatus != MachineHealthStatus.Healthy)
                {
                    // Trigger metric recovered event
                    _eventDispatcher.Dispatch(new MetricRecovered(machineId, metricName, value, limit));
                }
                violations[metricName] = status;
            }

            return status switch
            {
                MachineHealthStatus.Warning => 5.0,
                MachineHealthStatus.Critical => 15.0,
                MachineHealthStatus.Emergency => 30.0,
                _ => 0.0
            };
        }
    }

    /// <summary>
    /// Service coordinating real-time low-latency performance and session streaming telemetry.
    /// </summary>
    public class LiveMonitoringService : ILiveMonitoringService, ITelemetryAggregator
    {
        private readonly IPollingEngine _pollingEngine;
        private readonly IMonitoringPipeline _monitoringPipeline;
        private readonly IMonitoringCache _monitoringCache;
        private readonly ISamplingEngine _samplingEngine;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<LiveMonitoringService> _logger;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Func<HealthSnapshot, Task>>> _subscriptions = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _pollingLoops = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="LiveMonitoringService"/> class.
        /// </summary>
        public LiveMonitoringService(
            IPollingEngine pollingEngine,
            IMonitoringPipeline monitoringPipeline,
            IMonitoringCache monitoringCache,
            ISamplingEngine samplingEngine,
            IEventDispatcher eventDispatcher,
            ILogger<LiveMonitoringService> logger)
        {
            _pollingEngine = pollingEngine ?? throw new ArgumentNullException(nameof(pollingEngine));
            _monitoringPipeline = monitoringPipeline ?? throw new ArgumentNullException(nameof(monitoringPipeline));
            _monitoringCache = monitoringCache ?? throw new ArgumentNullException(nameof(monitoringCache));
            _samplingEngine = samplingEngine ?? throw new ArgumentNullException(nameof(samplingEngine));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task SubscribeLiveTelemetryAsync(string machineId, Func<HealthSnapshot, Task> onTelemetryReceived, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentException("Machine ID cannot be empty", nameof(machineId));
            if (onTelemetryReceived == null) throw new ArgumentNullException(nameof(onTelemetryReceived));

            var machineSubs = _subscriptions.GetOrAdd(machineId, _ => new ConcurrentDictionary<Guid, Func<HealthSnapshot, Task>>());
            var subId = Guid.NewGuid();
            machineSubs[subId] = onTelemetryReceived;

            _logger.LogInformation("Added subscriber {SubId} for workstation live telemetry stream on {MachineId}.", subId, machineId);

            // Start polling loop if not already running
            if (!_pollingLoops.ContainsKey(machineId))
            {
                var cts = new CancellationTokenSource();
                if (_pollingLoops.TryAdd(machineId, cts))
                {
                    _eventDispatcher.Dispatch(new MonitoringStarted(machineId));
                    _eventDispatcher.Dispatch(new MachineOnline(machineId));
                    _eventDispatcher.Dispatch(new ConnectionRestored(machineId));

                    Task.Run(() => ExecutingPollingLoopAsync(machineId, cts.Token), cts.Token);
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UnsubscribeLiveTelemetryAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentException("Machine ID cannot be empty", nameof(machineId));

            if (_subscriptions.TryRemove(machineId, out _))
            {
                _logger.LogInformation("Removed all subscribers for workstation live telemetry stream on {MachineId}.", machineId);
            }

            if (_pollingLoops.TryRemove(machineId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _eventDispatcher.Dispatch(new MonitoringStopped(machineId));
                _eventDispatcher.Dispatch(new MachineOffline(machineId));
                _eventDispatcher.Dispatch(new ConnectionLost(machineId, "Unsubscribed from monitoring stream."));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<MachineHealth> ProcessMetricsAsync(string machineId, IEnumerable<HealthSnapshot> snapshots, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentException("Machine ID cannot be empty", nameof(machineId));
            if (snapshots == null || !snapshots.Any()) throw new ArgumentException("Snapshots sequence cannot be empty", nameof(snapshots));

            var list = snapshots.ToList();
            double avgCpu = list.Average(s => s.CpuUtilization);
            double avgRam = list.Average(s => s.MemoryUtilization);

            double score = 100.0 - (avgCpu * 0.3) - (avgRam * 0.4);
            score = Math.Clamp(score, 0.0, 100.0);

            var health = new MachineHealth
            {
                MachineId = machineId,
                OverallHealthScore = score,
                ActiveWarningsCount = score < 90.0 ? 1 : 0,
                ActiveEmergenciesCount = score < 40.0 ? 1 : 0,
                SubsystemScores = new Dictionary<string, double>
                {
                    { "CPU", 100.0 - avgCpu },
                    { "Memory", 100.0 - avgRam }
                }
            };

            return Task.FromResult(health);
        }

        private async Task ExecutingPollingLoopAsync(string machineId, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // High-precision stopwatch measuring pipeline duration
                    var sw = Stopwatch.StartNew();

                    var rawSnapshot = await _pollingEngine.PollMetricsAsync(machineId, ct);
                    var processedSnapshot = await _monitoringPipeline.ProcessSnapshotAsync(rawSnapshot, ct);

                    var oldSnapshot = _monitoringCache.GetSnapshot(machineId);

                    _monitoringCache.SetSnapshot(machineId, processedSnapshot);

                    // Dispatch snapshot created and updated events
                    _eventDispatcher.Dispatch(new SnapshotCreated(machineId, processedSnapshot.SnapshotId));
                    _eventDispatcher.Dispatch(new SnapshotUpdated(machineId, processedSnapshot.SnapshotId));

                    // Check if overall health status changed to alert subscribers
                    if (oldSnapshot != null && oldSnapshot.OverallHealth != processedSnapshot.OverallHealth)
                    {
                        _eventDispatcher.Dispatch(new MachineHealthChanged(machineId, oldSnapshot.OverallHealth, processedSnapshot.OverallHealth, processedSnapshot.OverallHealthScore));
                    }

                    // Map to HealthSnapshot contract for callbacks
                    var hSnap = new HealthSnapshot
                    {
                        SnapshotId = processedSnapshot.SnapshotId,
                        TimestampUtc = processedSnapshot.TimestampUtc,
                        CpuUtilization = processedSnapshot.CpuUsage,
                        MemoryUtilization = processedSnapshot.MemoryPressurePercentage,
                        StorageUtilization = 100.0 - ((processedSnapshot.DiskFreeSpaceBytes / (processedSnapshot.DiskUsageBytes + processedSnapshot.DiskFreeSpaceBytes + 1.0)) * 100.0),
                        NetworkThroughputBytesPerSec = processedSnapshot.NetworkDownloadBytesPerSec + processedSnapshot.NetworkUploadBytesPerSec
                    };

                    if (_subscriptions.TryGetValue(machineId, out var machineSubs))
                    {
                        var subsTasks = machineSubs.Values.Select(async cb =>
                        {
                            try
                            {
                                await cb(hSnap);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Subscriber callback failed for machine {MachineId}.", machineId);
                            }
                        });
                        await Task.WhenAll(subsTasks);
                    }

                    sw.Stop();

                    // Query Sampling interval adaptively
                    int intervalMs = _samplingEngine.GetSamplingIntervalMs(machineId);
                    int delayMs = Math.Max(10, intervalMs - (int)sw.ElapsedMilliseconds);

                    await Task.Delay(delayMs, ct);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Polling loop cancelled for machine {MachineId}.", machineId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling loop encountered unhandled error for machine {MachineId}.", machineId);
            }
        }
    }

    /// <summary>
    /// Service implementing polling coordinators and triggering manual/scheduled sampling refresh cycles.
    /// </summary>
    public class MonitoringScheduler : IMonitoringScheduler
    {
        private readonly ILiveMonitoringService _liveMonitoring;
        private readonly IPollingEngine _pollingEngine;
        private readonly IMonitoringPipeline _monitoringPipeline;
        private readonly IMonitoringCache _monitoringCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoringScheduler"/> class.
        /// </summary>
        public MonitoringScheduler(
            ILiveMonitoringService liveMonitoring,
            IPollingEngine pollingEngine,
            IMonitoringPipeline monitoringPipeline,
            IMonitoringCache monitoringCache)
        {
            _liveMonitoring = liveMonitoring ?? throw new ArgumentNullException(nameof(liveMonitoring));
            _pollingEngine = pollingEngine ?? throw new ArgumentNullException(nameof(pollingEngine));
            _monitoringPipeline = monitoringPipeline ?? throw new ArgumentNullException(nameof(monitoringPipeline));
            _monitoringCache = monitoringCache ?? throw new ArgumentNullException(nameof(monitoringCache));
        }

        /// <inheritdoc />
        public void StartPolling(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            // Subscribe with a default empty task callback to keep the loop active
            _liveMonitoring.SubscribeLiveTelemetryAsync(machineId, _ => Task.CompletedTask);
        }

        /// <inheritdoc />
        public void StopPolling(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            _liveMonitoring.UnsubscribeLiveTelemetryAsync(machineId);
        }

        /// <inheritdoc />
        public async Task<LiveMonitoringSnapshot> TriggerManualRefreshAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentException("Machine ID cannot be empty", nameof(machineId));

            var raw = await _pollingEngine.PollMetricsAsync(machineId, ct);
            var processed = await _monitoringPipeline.ProcessSnapshotAsync(raw, ct);
            _monitoringCache.SetSnapshot(machineId, processed);
            return processed;
        }
    }

    /// <summary>
    /// API Query Service allowing advanced pagination, sorting, and filtering over cached live telemetry.
    /// </summary>
    public class LiveMonitoringQueryService : ILiveMonitoringQueryService
    {
        private readonly IMonitoringCache _cache;
        private readonly IAggregationEngine _aggregationEngine;

        /// <summary>
        /// Initializes a new instance of the <see cref="LiveMonitoringQueryService"/> class.
        /// </summary>
        public LiveMonitoringQueryService(IMonitoringCache cache, IAggregationEngine aggregationEngine)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _aggregationEngine = aggregationEngine ?? throw new ArgumentNullException(nameof(aggregationEngine));
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<LiveMonitoringSnapshot>> QueryCurrentMetricsAsync(
            Func<LiveMonitoringSnapshot, bool>? filter = null,
            string? sortBy = null,
            bool ascending = true,
            int pageIndex = 0,
            int pageSize = 10,
            CancellationToken ct = default)
        {
            var list = _cache.GetAllSnapshots().ToList();

            if (filter != null)
            {
                list = list.Where(filter).ToList();
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                Func<LiveMonitoringSnapshot, object> sortSelector = sortBy.ToLowerInvariant() switch
                {
                    "machineid" => s => s.MachineId,
                    "cpu" => s => s.CpuUsage,
                    "memory" => s => s.MemoryPressurePercentage,
                    "health" => s => s.OverallHealthScore,
                    _ => s => s.TimestampUtc
                };

                list = ascending
                    ? list.OrderBy(sortSelector).ToList()
                    : list.OrderByDescending(sortSelector).ToList();
            }

            var paged = list.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return Task.FromResult<IReadOnlyList<LiveMonitoringSnapshot>>(paged);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<LiveMonitoringSnapshot>> QueryHistoricalMetricsAsync(
            string machineId,
            DateTime? from = null,
            DateTime? to = null,
            int pageIndex = 0,
            int pageSize = 10,
            CancellationToken ct = default)
        {
            var history = _cache.GetHistory(machineId);

            if (from.HasValue)
            {
                history = history.Where(s => s.TimestampUtc >= from.Value).ToList();
            }
            if (to.HasValue)
            {
                history = history.Where(s => s.TimestampUtc <= to.Value).ToList();
            }

            var paged = history.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return Task.FromResult<IReadOnlyList<LiveMonitoringSnapshot>>(paged);
        }

        /// <inheritdoc />
        public Task<IDictionary<string, object>> QueryTrendsAsync(string machineId, string metricName, CancellationToken ct = default)
        {
            var history = _cache.GetHistory(machineId);
            var result = new Dictionary<string, object>();

            if (!history.Any() || string.IsNullOrEmpty(metricName))
            {
                result["Trend"] = "Stable";
                result["Average"] = 0.0;
                result["Max"] = 0.0;
                result["Min"] = 0.0;
                return Task.FromResult<IDictionary<string, object>>(result);
            }

            var values = history.Select(s => metricName.ToLowerInvariant() switch
            {
                "cpu" => s.CpuUsage,
                "memory" => s.MemoryPressurePercentage,
                "gpu" => s.GpuUsage,
                _ => s.CpuUsage
            }).ToList();

            result["Trend"] = _aggregationEngine.DetectTrend(values);
            result["Average"] = _aggregationEngine.ComputeMovingAverage(values);
            result["Max"] = values.Max();
            result["Min"] = values.Min();
            result["P95"] = _aggregationEngine.ComputePercentile(values, 95.0);

            return Task.FromResult<IDictionary<string, object>>(result);
        }
    }

    /// <summary>
    /// Service establishing secure administrative context, permissions verification, and visibility boundaries.
    /// </summary>
    public class LiveMonitoringSecurityService : ILiveMonitoringSecurityService
    {
        /// <inheritdoc />
        public Task<bool> ValidateVisibilityAsync(SecureMonitoringContext context, string machineId, CancellationToken ct = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrEmpty(machineId)) return Task.FromResult(false);

            // True if operator context is active and has valid credentials
            bool visible = context.IsAuthorized && !string.IsNullOrEmpty(context.OperatorId);
            return Task.FromResult(visible);
        }

        /// <inheritdoc />
        public Task<bool> CheckPermissionAsync(SecureMonitoringContext context, string requiredPermission, CancellationToken ct = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Validate permissions context
            bool allowed = context.IsAuthorized && !string.IsNullOrEmpty(requiredPermission);
            return Task.FromResult(allowed);
        }
    }
}
