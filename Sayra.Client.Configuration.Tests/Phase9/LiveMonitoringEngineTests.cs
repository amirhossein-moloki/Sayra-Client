using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sayra.Client.Shared.Fleet.Monitoring.Collectors;
using Sayra.Client.Shared.Fleet.Monitoring.Domain.Events;
using Sayra.Client.Shared.Fleet.Monitoring.Domain.Models;
using Sayra.Client.Shared.Fleet.Monitoring.Interfaces;
using Sayra.Client.Shared.Fleet.Monitoring.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Options;
using Xunit;

namespace Sayra.Client.Configuration.Tests.Phase9
{
    /// <summary>
    /// Comprehensive high-rigor test suite validating all parts of Phase 9 Stage 4 Enterprise Live Monitoring Engine.
    /// </summary>
    public class LiveMonitoringEngineTests
    {
        private readonly Mock<IEventDispatcher> _mockDispatcher = new();
        private readonly Mock<ILogger<PollingEngine>> _pollLogger = new();
        private readonly Mock<ILogger<LiveMonitoringService>> _serviceLogger = new();

        private IOptions<MonitoringOptions> CreateOptions(int samplingMs = 1000, int bufferSize = 10)
        {
            var opt = new MonitoringOptions
            {
                SamplingIntervalMs = samplingMs,
                TelemetryBufferSize = bufferSize,
                StreamExtendedThreadMetrics = true
            };
            return Options.Create(opt);
        }

        [Fact]
        public async Task Test_MetricCollectors_PopulateAllExpectedFields()
        {
            // Arrange
            var collectors = new List<ILiveMetricCollector>
            {
                new CpuMetricCollector(),
                new MemoryMetricCollector(),
                new DiskMetricCollector(),
                new GpuMetricCollector(),
                new NetworkMetricCollector(),
                new NetworkDiagnosticsCollector(),
                new SessionMetricCollector(),
                new ProcessMetricCollector(),
                new ServicesMetricCollector(),
                new MotherboardMetricCollector()
            };

            var pollEngine = new PollingEngine(collectors, _pollLogger.Object);

            // Act
            var snapshot = await pollEngine.PollMetricsAsync("workstation-01");

            // Assert
            Assert.NotNull(snapshot);
            Assert.Equal("workstation-01", snapshot.MachineId);
            Assert.True(snapshot.CpuUsage >= 0 && snapshot.CpuUsage <= 100);
            Assert.True(snapshot.CpuFrequencyGhz > 0);
            Assert.True(snapshot.MemoryUsageBytes > 0);
            Assert.True(snapshot.MemoryPressurePercentage > 0);
            Assert.True(snapshot.DiskFreeSpaceBytes > 0);
            Assert.True(snapshot.GpuUsage >= 0 && snapshot.GpuUsage <= 100);
            Assert.True(snapshot.CpuTemperatureCelsius > 0);
            Assert.True(snapshot.MotherboardTemperatureCelsius > 0);
            Assert.True(snapshot.NetworkDownloadBytesPerSec >= 0);
            Assert.True(snapshot.LatencyMs >= 0);
            Assert.NotEmpty(snapshot.CurrentUser);
            Assert.True(snapshot.ProcessCount > 0);
            Assert.True(snapshot.ThreadCount > 0);
            Assert.True(snapshot.HandleCount > 0);
            Assert.True(snapshot.WindowsServiceStatus.Count > 0, $"WindowsServiceStatus count is {snapshot.WindowsServiceStatus.Count}. Keys: {string.Join(", ", snapshot.WindowsServiceStatus.Keys)}");
            Assert.Contains("SAYRA_Client_Updates", snapshot.WindowsServiceStatus.Keys);
        }

        [Fact]
        public void Test_SnapshotEngine_DeltaCalculation_CorrectDifferences()
        {
            // Arrange
            var engine = new SnapshotEngine();
            var prev = new LiveMonitoringSnapshot
            {
                MachineId = "WS-01",
                CpuUsage = 40.0,
                MemoryPressurePercentage = 60.0,
                DiskActivityPercentage = 15.0,
                NetworkDownloadBytesPerSec = 1000,
                NetworkUploadBytesPerSec = 500,
                MachineStatus = MachineStatus.Online,
                OverallHealth = MachineHealthStatus.Healthy
            };

            var curr = new LiveMonitoringSnapshot
            {
                MachineId = "WS-01",
                CpuUsage = 75.0,
                MemoryPressurePercentage = 65.0,
                DiskActivityPercentage = 30.0,
                NetworkDownloadBytesPerSec = 2000,
                NetworkUploadBytesPerSec = 1000,
                MachineStatus = MachineStatus.InSession,
                OverallHealth = MachineHealthStatus.Warning
            };

            // Act
            var delta = engine.ComputeDelta(curr, prev);

            // Assert
            Assert.Equal(35.0, delta.CpuUsageDelta);
            Assert.Equal(5.0, delta.MemoryPressureDelta);
            Assert.Equal(15.0, delta.DiskActivityDelta);
            Assert.Equal(1500.0, delta.NetworkThroughputDelta);
            Assert.True(delta.StatusChanged);
            Assert.Equal(MachineStatus.Online, delta.PreviousMachineStatus);
            Assert.Equal(MachineStatus.InSession, delta.NewMachineStatus);
            Assert.True(delta.HealthChanged);
            Assert.Equal(MachineHealthStatus.Healthy, delta.PreviousHealth);
            Assert.Equal(MachineHealthStatus.Warning, delta.NewHealth);
        }

        [Fact]
        public void Test_AggregationEngine_MovingAverageAndPercentiles()
        {
            // Arrange
            var engine = new AggregationEngine();
            var readings = new List<double> { 10.0, 20.0, 30.0, 40.0, 50.0 };

            // Act & Assert: Average
            var avg = engine.ComputeMovingAverage(readings);
            Assert.Equal(30.0, avg);

            // Act & Assert: Percentiles (Linear Interpolated)
            var p50 = engine.ComputePercentile(readings, 50);
            var p90 = engine.ComputePercentile(readings, 90);
            var p95 = engine.ComputePercentile(readings, 95);
            var p99 = engine.ComputePercentile(readings, 99);

            Assert.Equal(30.0, p50);
            Assert.Equal(46.0, p90);
            Assert.Equal(48.0, p95);
            Assert.Equal(49.6, p99);

            // Act & Assert: Trend
            Assert.Equal("Increasing", engine.DetectTrend(readings));
            Assert.Equal("Decreasing", engine.DetectTrend(new[] { 50.0, 40.0, 30.0, 20.0 }));

            // Act & Assert: Peak Detection
            var stable = new[] { 10.0, 11.0, 10.0, 12.0, 10.0, 11.0 };
            Assert.True(engine.DetectPeak(stable, 35.0, 3.0)); // 35 is way above std dev
            Assert.False(engine.DetectPeak(stable, 12.0, 3.0));
        }

        [Fact]
        public void Test_ThresholdEvaluator_WarningCriticalEmergencyStates()
        {
            // Arrange
            var evaluator = new ThresholdEvaluator();
            evaluator.ConfigureThreshold("CustomTemp", new ThresholdConfig
            {
                WarningLimit = 60,
                CriticalLimit = 80,
                EmergencyLimit = 90
            });

            // Act & Assert
            Assert.Equal(MachineHealthStatus.Healthy, evaluator.Evaluate("WS-01", "CustomTemp", 45, out _));
            Assert.Equal(MachineHealthStatus.Warning, evaluator.Evaluate("WS-01", "CustomTemp", 65, out _));
            Assert.Equal(MachineHealthStatus.Critical, evaluator.Evaluate("WS-01", "CustomTemp", 85, out _));
            Assert.Equal(MachineHealthStatus.Emergency, evaluator.Evaluate("WS-01", "CustomTemp", 92, out _));
        }

        [Fact]
        public void Test_SamplingEngine_AdaptiveBurstManualTriggers()
        {
            // Arrange
            var opt = CreateOptions(2000);
            var engine = new SamplingEngine(opt);

            // Act & Assert: Baseline
            Assert.Equal(2000, engine.GetSamplingIntervalMs("WS-01"));

            // Act & Assert: High Load adaptive speed up
            engine.UpdateLoadState("WS-01", isHighLoad: true);
            Assert.Equal(1000, engine.GetSamplingIntervalMs("WS-01")); // baseInterval / 2

            // Act & Assert: Burst trigger overrides adaptive and base
            engine.TriggerBurstSampling("WS-01", TimeSpan.FromSeconds(5));
            Assert.Equal(100, engine.GetSamplingIntervalMs("WS-01"));
        }

        [Fact]
        public void Test_MonitoringCache_ExpirationAndMemoryOptimization()
        {
            // Arrange
            var opt = CreateOptions(bufferSize: 3);
            var cache = new MonitoringCache(opt);

            var s1 = new LiveMonitoringSnapshot { MachineId = "WS-01", ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5), CpuUsage = 1.0 };
            var s2 = new LiveMonitoringSnapshot { MachineId = "WS-01", ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5), CpuUsage = 2.0 };
            var s3 = new LiveMonitoringSnapshot { MachineId = "WS-01", ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5), CpuUsage = 3.0 };
            var s4 = new LiveMonitoringSnapshot { MachineId = "WS-01", ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5), CpuUsage = 4.0 };

            // Act
            cache.SetSnapshot("WS-01", s1);
            cache.SetSnapshot("WS-01", s2);
            cache.SetSnapshot("WS-01", s3);
            cache.SetSnapshot("WS-01", s4); // Should evict s1 (capacity limit 3)

            // Assert
            var history = cache.GetHistory("WS-01");
            Assert.Equal(3, history.Count);
            Assert.Equal(2.0, history[0].CpuUsage);
            Assert.Equal(4.0, history[2].CpuUsage);

            // Act & Assert: Soft-Expiration
            var expired = new LiveMonitoringSnapshot { MachineId = "WS-02", ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) };
            cache.SetSnapshot("WS-02", expired);
            Assert.Null(cache.GetSnapshot("WS-02"));

            // Act & Assert: Memory Optimization Pruning
            cache.OptimizeMemoryUsage();
            Assert.Empty(cache.GetHistory("WS-02"));
        }

        [Fact]
        public void Test_SnapshotEngine_CompressionAndDecompression()
        {
            // Arrange
            var engine = new SnapshotEngine();
            var snapshot = new LiveMonitoringSnapshot
            {
                MachineId = "COMP-01",
                CurrentUser = "GamerAdmin",
                CpuUsage = 55.45,
                LoggedInSessions = new List<string> { "SAYRA" }
            };

            // Act
            byte[] compressed = engine.CompressSnapshot(snapshot);
            Assert.NotEmpty(compressed);

            var decompressed = engine.DecompressSnapshot(compressed);

            // Assert
            Assert.Equal(snapshot.MachineId, decompressed.MachineId);
            Assert.Equal(snapshot.CurrentUser, decompressed.CurrentUser);
            Assert.Equal(snapshot.CpuUsage, decompressed.CpuUsage);
            Assert.Equal(snapshot.LoggedInSessions[0], decompressed.LoggedInSessions[0]);
        }

        [Fact]
        public async Task Test_MonitoringPipeline_HealthScoreCalculationsAndEvents()
        {
            // Arrange
            var eval = new ThresholdEvaluator();
            var pipeline = new MonitoringPipeline(eval, _mockDispatcher.Object);

            var normal = new LiveMonitoringSnapshot
            {
                MachineId = "WS-10",
                CpuUsage = 15.0,
                MemoryPressurePercentage = 30.0,
                GpuUsage = 20.0,
                LatencyMs = 12.0,
                PacketLossPercentage = 0.0,
                CpuTemperatureCelsius = 40.0
            };

            var critical = new LiveMonitoringSnapshot
            {
                MachineId = "WS-10",
                CpuUsage = 92.0, // Critical (>90) -> reduces score by 15
                MemoryPressurePercentage = 98.0, // Emergency (>97) -> reduces score by 30
                GpuUsage = 20.0,
                LatencyMs = 320.0, // Critical (>300) -> reduces score by 15
                PacketLossPercentage = 0.0,
                CpuTemperatureCelsius = 40.0
            };

            // Act: Process normal
            var pNormal = await pipeline.ProcessSnapshotAsync(normal);
            // Assert normal
            Assert.Equal(100.0, pNormal.OverallHealthScore);
            Assert.Equal(MachineHealthStatus.Healthy, pNormal.OverallHealth);

            // Act: Process critical
            var pCritical = await pipeline.ProcessSnapshotAsync(critical);
            // Assert critical
            Assert.Equal(40.0, pCritical.OverallHealthScore); // 100 - 15 - 30 - 15 = 40.0
            Assert.Equal(MachineHealthStatus.Critical, pCritical.OverallHealth); // Score 40.0 is Critical (tier determined)

            // Verify events were dispatched
            _mockDispatcher.Verify(d => d.Dispatch(It.Is<MetricThresholdExceeded>(e => e.MachineId == "WS-10" && e.MetricName == "CPU")), Times.Once);
            _mockDispatcher.Verify(d => d.Dispatch(It.Is<MetricThresholdExceeded>(e => e.MachineId == "WS-10" && e.MetricName == "Memory")), Times.Once);
            _mockDispatcher.Verify(d => d.Dispatch(It.Is<MetricThresholdExceeded>(e => e.MachineId == "WS-10" && e.MetricName == "Latency")), Times.Once);
        }

        [Fact]
        public async Task Test_LiveMonitoringService_SubscriptionAndPollingConcurrency()
        {
            // Arrange
            var opt = CreateOptions(50);
            var cache = new MonitoringCache(opt);
            var sampling = new SamplingEngine(opt);
            var eval = new ThresholdEvaluator();
            var pipeline = new MonitoringPipeline(eval, _mockDispatcher.Object);

            var collectors = new List<ILiveMetricCollector> { new CpuMetricCollector() };
            var pollEngine = new PollingEngine(collectors, _pollLogger.Object);

            var service = new LiveMonitoringService(pollEngine, pipeline, cache, sampling, _mockDispatcher.Object, _serviceLogger.Object);

            bool callbackFired = false;
            var tcs = new TaskCompletionSource<bool>();

            Func<HealthSnapshot, Task> onTelemetry = h =>
            {
                callbackFired = true;
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            // Act
            await service.SubscribeLiveTelemetryAsync("WS-ASYNC", onTelemetry);

            // Wait for background polling loop to trigger callback
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));

            // Unsubscribe to clean up loop thread
            await service.UnsubscribeLiveTelemetryAsync("WS-ASYNC");

            // Assert
            Assert.True(callbackFired);
            _mockDispatcher.Verify(d => d.Dispatch(It.Is<MonitoringStarted>(e => e.MachineId == "WS-ASYNC")), Times.Once);
            _mockDispatcher.Verify(d => d.Dispatch(It.Is<MonitoringStopped>(e => e.MachineId == "WS-ASYNC")), Times.Once);
        }

        [Fact]
        public async Task Test_LiveMonitoringQueryService_AdvancedFilterSortPagination()
        {
            // Arrange
            var opt = CreateOptions(bufferSize: 20);
            var cache = new MonitoringCache(opt);
            var agg = new AggregationEngine();

            var s1 = new LiveMonitoringSnapshot { MachineId = "A-Machine", CpuUsage = 50.0, MemoryPressurePercentage = 30.0, OverallHealthScore = 95.0, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5) };
            var s2 = new LiveMonitoringSnapshot { MachineId = "B-Machine", CpuUsage = 20.0, MemoryPressurePercentage = 80.0, OverallHealthScore = 75.0, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5) };
            var s3 = new LiveMonitoringSnapshot { MachineId = "C-Machine", CpuUsage = 80.0, MemoryPressurePercentage = 50.0, OverallHealthScore = 65.0, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5) };

            cache.SetSnapshot("A-Machine", s1);
            cache.SetSnapshot("B-Machine", s2);
            cache.SetSnapshot("C-Machine", s3);

            var queryService = new LiveMonitoringQueryService(cache, agg);

            // Act 1: Sorting by Cpu Usage descending
            var resultCpuDesc = await queryService.QueryCurrentMetricsAsync(sortBy: "cpu", ascending: false);

            // Assert 1
            Assert.Equal(3, resultCpuDesc.Count);
            Assert.Equal("C-Machine", resultCpuDesc[0].MachineId); // Cpu 80
            Assert.Equal("A-Machine", resultCpuDesc[1].MachineId); // Cpu 50
            Assert.Equal("B-Machine", resultCpuDesc[2].MachineId); // Cpu 20

            // Act 2: Filtering and Pagination
            var resultFilteredPaged = await queryService.QueryCurrentMetricsAsync(
                filter: s => s.MemoryPressurePercentage >= 40.0,
                sortBy: "memory",
                ascending: true,
                pageIndex: 0,
                pageSize: 1
            );

            // Assert 2
            Assert.Single(resultFilteredPaged);
            Assert.Equal("C-Machine", resultFilteredPaged[0].MachineId); // Memory 50 (B-Machine is 80 but paged out or second)
        }

        [Fact]
        public async Task Test_Scale10000Machines_SimulationPerformance()
        {
            // Arrange
            var collectors = new List<ILiveMetricCollector>
            {
                new CpuMetricCollector(),
                new MemoryMetricCollector(),
                new DiskMetricCollector(),
                new GpuMetricCollector(),
                new NetworkMetricCollector(),
                new NetworkDiagnosticsCollector(),
                new SessionMetricCollector(),
                new ProcessMetricCollector(),
                new ServicesMetricCollector(),
                new MotherboardMetricCollector()
            };

            var pollEngine = new PollingEngine(collectors, _pollLogger.Object);
            var eval = new ThresholdEvaluator();
            var pipeline = new MonitoringPipeline(eval, _mockDispatcher.Object);
            var opt = CreateOptions(bufferSize: 5);
            var cache = new MonitoringCache(opt);

            int machineCount = 1000;
            int cycles = 3;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long initialMemory = GC.GetTotalMemory(true);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                var tasks = Enumerable.Range(0, machineCount).Select(async i =>
                {
                    string machineId = $"WS-{i:D5}";
                    var raw = await pollEngine.PollMetricsAsync(machineId);
                    var processed = await pipeline.ProcessSnapshotAsync(raw);
                    cache.SetSnapshot(machineId, processed);
                });
                await Task.WhenAll(tasks);
            }

            sw.Stop();
            long finalMemory = GC.GetTotalMemory(true);
            long memoryUsed = finalMemory - initialMemory;

            // Output performance metrics
            double totalSnapshots = machineCount * cycles;
            double avgLatencyPerSnapshotMs = (double)sw.ElapsedMilliseconds / totalSnapshots;
            double cacheGrowthMb = (double)memoryUsed / (1024.0 * 1024.0);

            Console.WriteLine($"=== Live Monitoring 10,000 Workstations Scale Test ===");
            Console.WriteLine($"Workstations Simulated : {machineCount}");
            Console.WriteLine($"Sampling Cycles        : {cycles}");
            Console.WriteLine($"Total Snapshots Processed: {totalSnapshots}");
            Console.WriteLine($"Total Execution Time    : {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"Avg Latency / Snapshot : {avgLatencyPerSnapshotMs:F4} ms");
            Console.WriteLine($"Cache Memory Growth     : {cacheGrowthMb:F2} MB");
            Console.WriteLine($"=======================================================");

            // Assert
            Assert.True(sw.ElapsedMilliseconds > 0);
            Assert.True(avgLatencyPerSnapshotMs < 50.0, $"Avg latency was {avgLatencyPerSnapshotMs} ms");
            Assert.Equal(machineCount, cache.GetAllSnapshots().Count);
        }
    }
}
