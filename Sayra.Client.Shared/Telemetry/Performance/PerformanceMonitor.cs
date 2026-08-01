using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Telemetry.Performance
{
    /// <summary>
    /// Thread-safe enterprise implementation of the Performance Monitor engine.
    /// Tracks system-wide latency metrics, cache hit ratio, runtime details,
    /// and generates comprehensive performance snapshots.
    /// </summary>
    public class PerformanceMonitor : IPerformanceMonitor
    {
        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly ITracingService _tracingService;
        private readonly PerformanceOptions _options;

        // Thread-safe history of recent measurements for calculating latencies (simple lightweight rolling window of last 100 items)
        private readonly ConcurrentQueue<TimeSpan> _databaseLatencies = new();
        private readonly ConcurrentQueue<TimeSpan> _ipcLatencies = new();
        private readonly ConcurrentQueue<TimeSpan> _tcpLatencies = new();
        private readonly ConcurrentQueue<TimeSpan> _diskLatencies = new();
        private readonly ConcurrentQueue<TimeSpan> _workerExecutionTimes = new();

        private readonly ConcurrentDictionary<string, TimeSpan> _startupTimes = new(StringComparer.OrdinalIgnoreCase);

        private TimeSpan _lastAuthenticationTime = TimeSpan.Zero;
        private double _downloadSpeed;
        private double _uploadSpeed;
        private int _queueLength;

        // Cache hits/misses for hit ratio calculation
        private long _cacheHits;
        private long _cacheMisses;

        // Async operations count tracking
        private int _activeAsyncOps;

        // Latest recorded system snapshot
        private PerformanceSnapshot _latestSnapshot = new();
        private readonly SemaphoreSlim _snapshotLock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceMonitor"/> class.
        /// </summary>
        public PerformanceMonitor(
            ILogger<PerformanceMonitor> logger,
            ITracingService tracingService,
            IOptions<PerformanceOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tracingService = tracingService ?? throw new ArgumentNullException(nameof(tracingService));
            _options = options?.Value ?? new PerformanceOptions();
        }

        /// <inheritdoc />
        public async Task RecordPerformanceSnapshotAsync(PerformanceSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            cancellationToken.ThrowIfCancellationRequested();

            await _snapshotLock.WaitAsync(cancellationToken);
            try
            {
                _latestSnapshot = snapshot;
                _logger.LogDebug("Recorded new performance snapshot. Active Async Ops: {AsyncCount}, GC Count: {GcCount}",
                    snapshot.AsyncOperationsCount, snapshot.GarbageCollectionCount);
            }
            finally
            {
                _snapshotLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<PerformanceSnapshot> GetLatestPerformanceSnapshotAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Build fresh snapshot from live metrics to ensure accuracy
            var freshSnapshot = BuildLiveSnapshot();

            await _snapshotLock.WaitAsync(cancellationToken);
            try
            {
                // If a snapshot was explicitly recorded (indicated by non-default/non-live values in _latestSnapshot),
                // we should merge them so that non-zero/non-default values are preserved.
                _latestSnapshot = freshSnapshot with
                {
                    StartupTime = _latestSnapshot.StartupTime != TimeSpan.Zero ? _latestSnapshot.StartupTime : freshSnapshot.StartupTime,
                    AuthenticationTime = _latestSnapshot.AuthenticationTime != TimeSpan.Zero ? _latestSnapshot.AuthenticationTime : freshSnapshot.AuthenticationTime,
                    DatabaseLatency = _latestSnapshot.DatabaseLatency != TimeSpan.Zero ? _latestSnapshot.DatabaseLatency : freshSnapshot.DatabaseLatency,
                    IpcLatency = _latestSnapshot.IpcLatency != TimeSpan.Zero ? _latestSnapshot.IpcLatency : freshSnapshot.IpcLatency,
                    TcpLatency = _latestSnapshot.TcpLatency != TimeSpan.Zero ? _latestSnapshot.TcpLatency : freshSnapshot.TcpLatency,
                    DownloadSpeed = _latestSnapshot.DownloadSpeed > 0 ? _latestSnapshot.DownloadSpeed : freshSnapshot.DownloadSpeed,
                    UploadSpeed = _latestSnapshot.UploadSpeed > 0 ? _latestSnapshot.UploadSpeed : freshSnapshot.UploadSpeed,
                    DiskLatency = _latestSnapshot.DiskLatency != TimeSpan.Zero ? _latestSnapshot.DiskLatency : freshSnapshot.DiskLatency,
                    CacheHitRatio = _latestSnapshot.CacheHitRatio > 0 ? _latestSnapshot.CacheHitRatio : freshSnapshot.CacheHitRatio,
                    QueueLength = _latestSnapshot.QueueLength > 0 ? _latestSnapshot.QueueLength : freshSnapshot.QueueLength,
                    WorkerExecutionTime = _latestSnapshot.WorkerExecutionTime != TimeSpan.Zero ? _latestSnapshot.WorkerExecutionTime : freshSnapshot.WorkerExecutionTime,
                    GarbageCollectionCount = _latestSnapshot.GarbageCollectionCount > 0 ? _latestSnapshot.GarbageCollectionCount : freshSnapshot.GarbageCollectionCount,
                    ThreadPoolThreads = _latestSnapshot.ThreadPoolThreads > 0 ? _latestSnapshot.ThreadPoolThreads : freshSnapshot.ThreadPoolThreads,
                    AsyncOperationsCount = _latestSnapshot.AsyncOperationsCount > 0 ? _latestSnapshot.AsyncOperationsCount : freshSnapshot.AsyncOperationsCount,
                };
                return _latestSnapshot;
            }
            finally
            {
                _snapshotLock.Release();
            }
        }

        /// <inheritdoc />
        public IPerformanceMeasurement StartMeasurement(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("Operation name cannot be null or whitespace.", nameof(operationName));
            }

            // Capture tracing context if available
            string? traceId = _tracingService.CurrentContext?.TraceId?.Value;
            string? correlationId = _tracingService.CurrentContext?.CorrelationId?.Value;

            IncrementActiveAsyncOperations();

            return new PerformanceMeasurementScope(this, operationName, traceId, correlationId);
        }

        /// <inheritdoc />
        public void RecordMeasurement(IPerformanceMeasurement measurement)
        {
            if (measurement == null) return;

            DecrementActiveAsyncOperations();

            // Direct mapping of operation category based on name convention
            var opName = measurement.OperationName;

            if (opName.StartsWith("Database", StringComparison.OrdinalIgnoreCase))
            {
                AddToQueue(_databaseLatencies, measurement.Duration);
            }
            else if (opName.StartsWith("Ipc", StringComparison.OrdinalIgnoreCase))
            {
                AddToQueue(_ipcLatencies, measurement.Duration);
            }
            else if (opName.StartsWith("Tcp", StringComparison.OrdinalIgnoreCase) || opName.StartsWith("Network.Tcp", StringComparison.OrdinalIgnoreCase))
            {
                AddToQueue(_tcpLatencies, measurement.Duration);
            }
            else if (opName.StartsWith("Disk", StringComparison.OrdinalIgnoreCase) || opName.StartsWith("Storage.Disk", StringComparison.OrdinalIgnoreCase))
            {
                AddToQueue(_diskLatencies, measurement.Duration);
            }
            else if (opName.StartsWith("Worker", StringComparison.OrdinalIgnoreCase) || opName.StartsWith("BackgroundWorker", StringComparison.OrdinalIgnoreCase))
            {
                AddToQueue(_workerExecutionTimes, measurement.Duration);
            }
            else if (opName.StartsWith("Authentication", StringComparison.OrdinalIgnoreCase))
            {
                _lastAuthenticationTime = measurement.Duration;
            }
            else if (opName.StartsWith("Startup.", StringComparison.OrdinalIgnoreCase))
            {
                var stageName = opName.Substring("Startup.".Length);
                _startupTimes[stageName] = measurement.Duration;
            }

            // Flag high latencies exceeding the threshold
            if (measurement.Duration.TotalMilliseconds > _options.LatencyWarningThresholdMilliseconds)
            {
                _logger.LogWarning("Performance Alert: Latency warning threshold exceeded. Operation: {OperationName}, Duration: {Duration}ms (Threshold: {Threshold}ms)",
                    opName, measurement.Duration.TotalMilliseconds, _options.LatencyWarningThresholdMilliseconds);
            }
        }

        // --- Live Setters for specialized monitor services ---

        public void RecordDownloadSpeed(double bytesPerSecond) => _downloadSpeed = bytesPerSecond;

        public void RecordUploadSpeed(double bytesPerSecond) => _uploadSpeed = bytesPerSecond;

        public void RecordQueueLength(int length) => _queueLength = length;

        public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);

        public void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);

        public void RecordStartupStage(string stageName, TimeSpan duration)
        {
            if (string.IsNullOrWhiteSpace(stageName)) return;
            _startupTimes[stageName] = duration;
        }

        public void IncrementActiveAsyncOperations() => Interlocked.Increment(ref _activeAsyncOps);

        public void DecrementActiveAsyncOperations() => Interlocked.Decrement(ref _activeAsyncOps);

        // --- Private Helpers ---

        private static void AddToQueue(ConcurrentQueue<TimeSpan> queue, TimeSpan duration)
        {
            queue.Enqueue(duration);
            while (queue.Count > 100)
            {
                queue.TryDequeue(out _);
            }
        }

        private static TimeSpan GetAverageDuration(ConcurrentQueue<TimeSpan> queue)
        {
            if (queue.IsEmpty) return TimeSpan.Zero;
            var list = queue.ToList();
            if (list.Count == 0) return TimeSpan.Zero;
            var averageTicks = (long)list.Average(t => t.Ticks);
            return TimeSpan.FromTicks(averageTicks);
        }

        private PerformanceSnapshot BuildLiveSnapshot()
        {
            // Thread Pool Live metrics
            ThreadPool.GetMaxThreads(out int maxWorker, out int _);
            ThreadPool.GetAvailableThreads(out int availWorker, out int _);
            int busyThreadPoolThreads = Math.Max(0, maxWorker - availWorker);

            // GC Collections
            int totalGcCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

            // Startup Time Calculation (Sum of tracked startup stages or last tracked total Application startup)
            TimeSpan startupTime = TimeSpan.Zero;
            if (_startupTimes.TryGetValue("Application", out var appStartupTime))
            {
                startupTime = appStartupTime;
            }
            else if (_startupTimes.Count > 0)
            {
                long ticksSum = _startupTimes.Values.Sum(v => v.Ticks);
                startupTime = TimeSpan.FromTicks(ticksSum);
            }

            // Cache Hit Ratio
            double hitRatio = 1.0;
            long hits = Interlocked.Read(ref _cacheHits);
            long misses = Interlocked.Read(ref _cacheMisses);
            long totalRequests = hits + misses;
            if (totalRequests > 0)
            {
                hitRatio = (double)hits / totalRequests;
            }

            return new PerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                StartupTime = startupTime,
                AuthenticationTime = _lastAuthenticationTime,
                DatabaseLatency = GetAverageDuration(_databaseLatencies),
                IpcLatency = GetAverageDuration(_ipcLatencies),
                TcpLatency = GetAverageDuration(_tcpLatencies),
                DownloadSpeed = _downloadSpeed,
                UploadSpeed = _uploadSpeed,
                DiskLatency = GetAverageDuration(_diskLatencies),
                CacheHitRatio = hitRatio,
                QueueLength = _queueLength,
                WorkerExecutionTime = GetAverageDuration(_workerExecutionTimes),
                GarbageCollectionCount = totalGcCount,
                ThreadPoolThreads = busyThreadPoolThreads,
                AsyncOperationsCount = Volatile.Read(ref _activeAsyncOps),
                MachineId = Environment.MachineName
            };
        }
    }
}
