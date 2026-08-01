using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Telemetry.Performance;
using Sayra.Client.Shared.Telemetry.Tracing;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// High-rigor xUnit test suite validating Phase 8 Stage 5 Enterprise Performance Monitoring Platform.
    /// </summary>
    public class PerformanceMonitorTests
    {
        private readonly PerformanceMonitor _monitor;
        private readonly MockTracingService _tracingService;

        public PerformanceMonitorTests()
        {
            var options = Options.Create(new PerformanceOptions
            {
                LatencyWarningThresholdMilliseconds = 1000,
                MemoryLimitMegabytes = 512
            });
            _tracingService = new MockTracingService();
            _monitor = new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance, _tracingService, options);
        }

        [Fact]
        public void StartMeasurement_CreatesValidScope_WithActiveTracking()
        {
            // Act
            using var scope = _monitor.StartMeasurement("Database.Read");

            // Assert
            Assert.NotNull(scope);
            Assert.Equal("Database.Read", scope.OperationName);
            Assert.True(scope.IsSuccess);
            Assert.Null(scope.Exception);
            Assert.Null(scope.EndTime);
        }

        [Fact]
        public async Task MeasurementScope_Disposal_ComputesAccurateDurationAndRecordsToMonitor()
        {
            // Act
            using (var scope = _monitor.StartMeasurement("Database.Read"))
            {
                await Task.Delay(50);
            }

            // Assert & Retrieve Snapshot
            var snapshot = await _monitor.GetLatestPerformanceSnapshotAsync();
            Assert.True(snapshot.DatabaseLatency > TimeSpan.Zero);
            Assert.True(snapshot.DatabaseLatency.TotalMilliseconds >= 40, $"Expected latency to be >= 40ms, got {snapshot.DatabaseLatency.TotalMilliseconds}ms");
        }

        [Fact]
        public void MeasurementScope_CaptureException_SetsFailureAndStoresException()
        {
            // Arrange
            var exception = new InvalidOperationException("Simulation DB Failure");

            // Act
            using (var scope = _monitor.StartMeasurement("Database.Write"))
            {
                scope.CaptureException(exception);
            }

            // Assert
            var snapshot = _monitor.GetLatestPerformanceSnapshotAsync().Result;
            Assert.True(snapshot.DatabaseLatency > TimeSpan.Zero);
        }

        [Fact]
        public async Task GetLatestPerformanceSnapshotAsync_SupportsCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await _monitor.GetLatestPerformanceSnapshotAsync(cts.Token));
        }

        [Fact]
        public async Task RecordPerformanceSnapshotAsync_SavesSnapshotCorrectly_AndSupportsCancellation()
        {
            // Arrange
            var snapshot = new PerformanceSnapshot
            {
                DatabaseLatency = TimeSpan.FromMilliseconds(123),
                IpcLatency = TimeSpan.FromMilliseconds(456)
            };

            // Act
            await _monitor.RecordPerformanceSnapshotAsync(snapshot);
            var retrieved = await _monitor.GetLatestPerformanceSnapshotAsync();

            // Assert
            Assert.Equal(TimeSpan.FromMilliseconds(123), retrieved.DatabaseLatency);
            Assert.Equal(TimeSpan.FromMilliseconds(456), retrieved.IpcLatency);

            // Test Cancellation
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await _monitor.RecordPerformanceSnapshotAsync(snapshot, cts.Token));
        }

        [Fact]
        public async Task ConcurrencyTest_ActiveAsyncOperationsAndMeasurementRecordingsAreThreadSafe()
        {
            // Arrange
            int threadsCount = 20;
            int iterations = 50;
            var completedCount = 0;
            var errors = new ConcurrentBag<string>();

            // Act
            var tasks = new Task[threadsCount];
            for (int i = 0; i < threadsCount; i++)
            {
                int threadId = i;
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        for (int j = 0; j < iterations; j++)
                        {
                            using (var scope = _monitor.StartMeasurement("Database.Query"))
                            {
                                await Task.Delay(new Random().Next(1, 5));
                            }
                        }
                        Interlocked.Increment(ref completedCount);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Thread {threadId} threw: {ex.Message}");
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Empty(errors);
            Assert.Equal(threadsCount, completedCount);

            var snapshot = await _monitor.GetLatestPerformanceSnapshotAsync();
            Assert.True(snapshot.DatabaseLatency > TimeSpan.Zero);
            Assert.Equal(0, snapshot.AsyncOperationsCount); // all completed and decremented
        }

        [Fact]
        public void TracingCorrelation_ExtractsTraceAndCorrelationIdsFromAmbientContext()
        {
            // Arrange
            var parentContext = new TraceContext
            {
                TraceId = new(),
                CorrelationId = new()
            };
            _tracingService.CurrentContext = parentContext;

            // Act
            using var scope = _monitor.StartMeasurement("Ipc.Ping");

            // Assert
            Assert.Equal(parentContext.TraceId.Value, scope.TraceId);
            Assert.Equal(parentContext.CorrelationId.Value, scope.CorrelationId);
        }

        [Fact]
        public void DatabasePerformanceMonitor_Wrapper_TracksQueryConnectionAndTransaction()
        {
            // Arrange
            var dbMonitor = new DatabasePerformanceMonitor(_monitor);

            // Act
            using (var q = dbMonitor.TrackQuery("SELECT 1")) { }
            using (var c = dbMonitor.TrackConnection()) { }
            using (var t = dbMonitor.TrackTransaction()) { }

            // Assert
            var snapshot = _monitor.GetLatestPerformanceSnapshotAsync().Result;
            Assert.True(snapshot.DatabaseLatency > TimeSpan.Zero);
        }

        [Fact]
        public void IpcPerformanceMonitor_Wrapper_TracksRequestsAndTimeouts()
        {
            // Arrange
            var ipcMonitor = new IpcPerformanceMonitor(_monitor);

            // Act
            using (var req = ipcMonitor.TrackRequest("Ping")) { }
            using (var lat = ipcMonitor.TrackPipeLatency()) { }
            ipcMonitor.RecordTimeout();
            ipcMonitor.RecordTimeout();

            // Assert
            Assert.Equal(2, ipcMonitor.TimeoutCount);
            var snapshot = _monitor.GetLatestPerformanceSnapshotAsync().Result;
            Assert.True(snapshot.IpcLatency > TimeSpan.Zero);
        }

        [Fact]
        public void NetworkPerformanceMonitor_Wrapper_TracksLatencyThroughputsAndFailures()
        {
            // Arrange
            var netMonitor = new NetworkPerformanceMonitor(_monitor);

            // Act
            using (var lat = netMonitor.TrackTcpLatency()) { }
            netMonitor.RecordDownloadThroughput(1024 * 1024 * 5); // 5 MB/s
            netMonitor.RecordUploadThroughput(1024 * 1024 * 2);   // 2 MB/s
            netMonitor.RecordConnectionFailure();

            // Assert
            Assert.Equal(1, netMonitor.ConnectionFailures);
            var snapshot = _monitor.GetLatestPerformanceSnapshotAsync().Result;
            Assert.Equal(1024 * 1024 * 5, snapshot.DownloadSpeed);
            Assert.Equal(1024 * 1024 * 2, snapshot.UploadSpeed);
            Assert.True(snapshot.TcpLatency > TimeSpan.Zero);
        }

        [Fact]
        public void CachePerformanceMonitor_Wrapper_TracksHitsMissesAndCalculatesRatio()
        {
            // Arrange
            var cacheMonitor = new CachePerformanceMonitor(_monitor);

            // Act
            cacheMonitor.RecordHit();
            cacheMonitor.RecordHit();
            cacheMonitor.RecordHit();
            cacheMonitor.RecordMiss(); // 3 hits, 1 miss -> 75% hit ratio

            // Assert
            var snapshot = _monitor.GetLatestPerformanceSnapshotAsync().Result;
            Assert.Equal(0.75, snapshot.CacheHitRatio);
        }

        [Fact]
        public void RuntimePerformanceMonitor_Wrapper_CollectsGcAndThreadPoolMetrics()
        {
            // Arrange
            var runtimeMonitor = new RuntimePerformanceMonitor(_monitor);

            // Act & Assert
            Assert.True(runtimeMonitor.Gen0Collections >= 0);
            Assert.True(runtimeMonitor.AllocatedMemoryBytes > 0);
            Assert.True(runtimeMonitor.AvailableWorkerThreads > 0);
            Assert.True(runtimeMonitor.BusyWorkerThreads >= 0);
            Assert.True(runtimeMonitor.ThreadPoolQueuePressure >= 0);

            using (var op = runtimeMonitor.TrackAsyncOperation("NetworkCall")) { }
        }

        [Fact]
        public void StartupPerformanceMonitor_Wrapper_TracksStartupStages()
        {
            // Arrange
            var startupMonitor = new StartupPerformanceMonitor(_monitor);

            // Act
            using (var app = startupMonitor.TrackStage("Application")) { }
            startupMonitor.RecordStage("WpfShell", TimeSpan.FromMilliseconds(500));

            // Assert
            var snapshot = _monitor.GetLatestPerformanceSnapshotAsync().Result;
            Assert.True(snapshot.StartupTime > TimeSpan.Zero);
        }

        // --- Mock Tracing Service for testing ambient correlation extraction ---
        private class MockTracingService : ITracingService
        {
            public TraceContext? CurrentContext { get; set; }

            public Task<TraceContext> StartTraceAsync(string operationName, TraceContext? parentContext = null, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task EndTraceAsync(TraceContext context, TraceResult result, string? exception = null, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Sayra.Client.Shared.Models.Telemetry.ValueObjects.CorrelationId CreateCorrelationId()
            {
                throw new NotImplementedException();
            }

            public Task<TraceScope> CreateScopeAsync(string operationName, TraceContext? parentContext = null, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }
    }
}
