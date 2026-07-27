using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;
using Sayra.Client.Diagnostics.Telemetry;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    public class LiveTelemetryTests
    {
        private readonly Mock<ILogger<LiveTelemetryService>> _serviceLogger = new();
        private readonly Mock<ILogger<CpuTelemetryCollector>> _cpuLogger = new();
        private readonly Mock<ILogger<MemoryTelemetryCollector>> _memLogger = new();
        private readonly Mock<ILogger<GpuTelemetryCollector>> _gpuLogger = new();
        private readonly Mock<ILogger<HardwareHealthCollector>> _healthLogger = new();
        private readonly Mock<ILogger<NetworkTelemetryCollector>> _netLogger = new();
        private readonly Mock<ILogger<StorageTelemetryCollector>> _storageLogger = new();
        private readonly Mock<ILogger<SessionTelemetryCollector>> _sessionLogger = new();
        private readonly Mock<ILogger<DiagnosticsEngine>> _diagLogger = new();
        private readonly Mock<ILogger<SoftwareInventoryCollector>> _softLogger = new();
        private readonly Mock<ILogger<ProcessInventoryCollector>> _procLogger = new();
        private readonly Mock<ILogger<DriverInventoryCollector>> _driverLogger = new();

        [Fact]
        public async Task Test_SnapshotGeneration_ConcurrentlyAggregatesAllCollectors()
        {
            // Arrange
            var collectors = new List<ITelemetryCollector>();
            var data = new LiveTelemetryData();

            var collector1 = new Mock<ITelemetryCollector>();
            collector1.Setup(c => c.CollectAsync(It.IsAny<LiveTelemetryData>(), It.IsAny<CancellationToken>()))
                      .Callback<LiveTelemetryData, CancellationToken>((d, t) => d.CpuUsagePercent = 45.2)
                      .Returns(Task.CompletedTask);

            var collector2 = new Mock<ITelemetryCollector>();
            collector2.Setup(c => c.CollectAsync(It.IsAny<LiveTelemetryData>(), It.IsAny<CancellationToken>()))
                      .Callback<LiveTelemetryData, CancellationToken>((d, t) => d.RamUsedMb = 2048)
                      .Returns(Task.CompletedTask);

            collectors.Add(collector1.Object);
            collectors.Add(collector2.Object);

            var service = new LiveTelemetryService(collectors, _serviceLogger.Object);

            // Act
            var snapshot = await service.CaptureSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.Equal(45.2, snapshot.CpuUsagePercent);
            Assert.Equal(2048, snapshot.RamUsedMb);
            Assert.NotEmpty(snapshot.MachineId);
        }

        [Fact]
        public async Task Test_CollectorFailureHandling_DoesNotStopTelemetry()
        {
            // Arrange
            var collectors = new List<ITelemetryCollector>();

            var healthyCollector = new Mock<ITelemetryCollector>();
            healthyCollector.Setup(c => c.CollectAsync(It.IsAny<LiveTelemetryData>(), It.IsAny<CancellationToken>()))
                            .Callback<LiveTelemetryData, CancellationToken>((d, t) => d.CpuUsagePercent = 10.0)
                            .Returns(Task.CompletedTask);

            var failingCollector = new Mock<ITelemetryCollector>();
            failingCollector.Setup(c => c.CollectAsync(It.IsAny<LiveTelemetryData>(), It.IsAny<CancellationToken>()))
                            .ThrowsAsync(new InvalidOperationException("Sensor hardware offline!"));

            collectors.Add(healthyCollector.Object);
            collectors.Add(failingCollector.Object);

            var service = new LiveTelemetryService(collectors, _serviceLogger.Object);

            // Act
            var snapshot = await service.CaptureSnapshotAsync();

            // Assert: Snapshot is still produced, failing collector doesn't crash the pipeline
            Assert.NotNull(snapshot);
            Assert.Equal(10.0, snapshot.CpuUsagePercent);
        }

        [Fact]
        public async Task Test_CancellationSupport_RespectsCancellationToken()
        {
            // Arrange
            var collectors = new List<ITelemetryCollector>();
            var collector = new Mock<ITelemetryCollector>();
            collector.Setup(c => c.CollectAsync(It.IsAny<LiveTelemetryData>(), It.IsAny<CancellationToken>()))
                     .Returns<LiveTelemetryData, CancellationToken>((d, ct) => Task.Delay(5000, ct)); // long running

            collectors.Add(collector.Object);
            var service = new LiveTelemetryService(collectors, _serviceLogger.Object);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(50);

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await service.CaptureSnapshotAsync(cts.Token);
            });
        }

        [Fact]
        public async Task Test_CpuTelemetryCollector_ValidReadings()
        {
            // Arrange
            var mockPerf = new Mock<IPerformanceCounterProvider>();
            mockPerf.Setup(p => p.GetCpuUsage()).Returns(35.5f);

            var mockCpu = new Mock<ICpuProvider>();
            mockCpu.Setup(c => c.GetCpuAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new CpuInformation("AMD Ryzen 9", "AMD", "x64", 16, 8, 16, 3.7, 3.7, 0, 0, 0, "AVX2", true));

            var collector = new CpuTelemetryCollector(mockPerf.Object, mockCpu.Object, _cpuLogger.Object);
            var data = new LiveTelemetryData();

            // Act
            await collector.CollectAsync(data);

            // Assert
            Assert.Equal(35.5, data.CpuUsagePercent);
        }

        [Fact]
        public async Task Test_MemoryTelemetryCollector_CorrectCalculation()
        {
            // Arrange
            var mockMem = new Mock<IMemoryProvider>();
            long totalBytes = 16L * 1024 * 1024 * 1024; // 16GB
            long usedBytes = 6L * 1024 * 1024 * 1024; // 6GB
            long availBytes = totalBytes - usedBytes;

            mockMem.Setup(m => m.GetMemoryAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new MemoryInformation(totalBytes, availBytes, "DDR4", 3200, usedBytes, 2, false, new()));

            var collector = new MemoryTelemetryCollector(mockMem.Object, _memLogger.Object);
            var data = new LiveTelemetryData();

            // Act
            await collector.CollectAsync(data);

            // Assert
            Assert.Equal(16384.0, data.RamTotalMb);
            Assert.Equal(6144.0, data.RamUsedMb);
        }

        [Fact]
        public async Task Test_NetworkTelemetryCollector_PingTimeoutAndTimeoutHandling()
        {
            // Arrange
            var collector = new NetworkTelemetryCollector(_netLogger.Object)
            {
                TargetAddress = "127.0.0.1",
                PingTimeoutMs = 100
            };
            var data = new LiveTelemetryData();

            // Act
            await collector.CollectAsync(data);

            // Assert: Does not throw even on ping timeouts/errors
            Assert.True(data.PingMs >= 0);
        }

        [Fact]
        public async Task Test_DiagnosticsEngine_GeneratesFullReportWithInventories()
        {
            // Arrange
            var mockCpu = new Mock<ICpuProvider>();
            mockCpu.Setup(c => c.GetCpuAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new CpuInformation("AMD Ryzen 9", "AMD", "x64", 16, 8, 16, 3.7, 3.7, 0, 0, 0, "AVX2", true));

            var mockGpu = new Mock<IGpuProvider>();
            mockGpu.Setup(g => g.GetGpusAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<GpuInformation> { new GpuInformation("RTX 4090", "NVIDIA", "551.23", 24L * 1024 * 1024 * 1024, 0, "", "", "", "", 144) });

            var mockMem = new Mock<IMemoryProvider>();
            mockMem.Setup(m => m.GetMemoryAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new MemoryInformation(16L * 1024 * 1024 * 1024, 10L * 1024 * 1024 * 1024, "DDR4", 3200, 6L * 1024 * 1024 * 1024, 2, false, new()));

            var mockStorage = new Mock<IStorageProvider>();
            mockStorage.Setup(s => s.GetStorageAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<StorageInformation> { new StorageInformation("SSD", 1000L * 1024 * 1024 * 1024, 500L * 1024 * 1024 * 1024, "NTFS", "C:", "OS", "NTFS", 500L * 1024 * 1024 * 1024, "Healthy", "123") });

            var mockNet = new Mock<INetworkProvider>();
            mockNet.Setup(n => n.GetNetworksAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<NetworkInformation> { new NetworkInformation("localhost", "127.0.0.1", "::1", "00:00:00:00:00:00", "Loopback", "Ethernet", "127.0.0.1", "127.0.0.1", "Connected", 10000000) });

            var wmiMock = new Mock<IWmiProvider>();

            var softCollector = new SoftwareInventoryCollector(_softLogger.Object);
            var procCollector = new ProcessInventoryCollector(_procLogger.Object);
            var driverCollector = new DriverInventoryCollector(wmiMock.Object, _driverLogger.Object);

            var engine = new DiagnosticsEngine(
                mockCpu.Object,
                mockGpu.Object,
                mockMem.Object,
                mockStorage.Object,
                mockNet.Object,
                softCollector,
                procCollector,
                driverCollector,
                _diagLogger.Object
            );

            // Act
            var report = await engine.GenerateFullReportAsync();

            // Assert
            Assert.NotNull(report);
            Assert.Equal("AMD Ryzen 9", report.Cpu.Name);
            Assert.Single(report.Gpus);
            Assert.Equal("RTX 4090", report.Gpus[0].Name);
            Assert.NotEmpty(report.SoftwareInventory);
            Assert.NotEmpty(report.ProcessInventory);
            Assert.NotEmpty(report.DriverInventory);
        }

        [Fact]
        public void Test_ProcessInventoryCollector_Sha256HashGeneration()
        {
            // Arrange
            var collector = new ProcessInventoryCollector(_procLogger.Object);
            string testFile = Path.Combine(Path.GetTempPath(), $"telemetry_test_{Guid.NewGuid()}.txt");
            File.WriteAllText(testFile, "SAYRA Live Telemetry Engine Test Payload!");

            try
            {
                // Act
                string hash = collector.CalculateFileHash(testFile);

                // Assert
                Assert.NotNull(hash);
                Assert.Equal(64, hash.Length); // standard SHA-256 string is 64 characters
            }
            finally
            {
                if (File.Exists(testFile)) File.Delete(testFile);
            }
        }

        [Fact]
        public async Task Test_GpuTelemetryCollector_MissingGpu_ReturnsUnknown()
        {
            // Arrange
            var mockGpu = new Mock<IGpuProvider>();
            mockGpu.Setup(g => g.GetGpusAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<GpuInformation>()); // empty list (no GPUs found)

            var collector = new GpuTelemetryCollector(mockGpu.Object, _gpuLogger.Object);
            var data = new LiveTelemetryData();

            // Act
            await collector.CollectAsync(data);

            // Assert
            Assert.Equal(0, data.GpuUsagePercent);
            Assert.Equal(0, data.VramTotalMb);
            Assert.Equal(0, data.VramUsedMb);
            Assert.Equal(0, data.GpuTemperature);
        }

        [Fact]
        public async Task Test_HardwareHealthCollector_MissingSensorProvider_UsesDefaults()
        {
            // Arrange: Null sensor provider passed in constructor
            var collector = new HardwareHealthCollector(_healthLogger.Object, sensorProvider: null);
            var data = new LiveTelemetryData();

            // Act
            await collector.CollectAsync(data);

            // Assert
            Assert.Equal(0, data.CpuTemperature);
            Assert.Equal(0, data.GpuTemperature);
            Assert.Equal(0, data.FanSpeed);
        }

        [Fact]
        public void Test_ProcessInventoryCollector_PermissionDenied_DoesNotCrash()
        {
            // Arrange
            var collector = new ProcessInventoryCollector(_procLogger.Object);

            // Act: Calculate hash of a non-existent or locked file
            string result = collector.CalculateFileHash(@"C:\System Volume Information\locked.sys");

            // Assert
            Assert.Contains("Denied", result); // returns descriptive error string rather than crashing
        }

        [Fact]
        public async Task Test_HardwareApiUnavailable_ThrowsException_HandledGracefully()
        {
            // Arrange
            var mockCpu = new Mock<ICpuProvider>();
            mockCpu.Setup(c => c.GetCpuAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new TimeoutException("WMI infrastructure blocked or locked."));

            var mockPerf = new Mock<IPerformanceCounterProvider>();
            var collector = new CpuTelemetryCollector(mockPerf.Object, mockCpu.Object, _cpuLogger.Object);
            var data = new LiveTelemetryData();

            // Act
            await collector.CollectAsync(data);

            // Assert: Handles the exception without throwing it, setting standard fallback CPU reading
            Assert.Equal(12.5, data.CpuUsagePercent);
        }
    }
}
