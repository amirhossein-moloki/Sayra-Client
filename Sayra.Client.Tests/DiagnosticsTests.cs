using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sayra.Client.Diagnostics.Configuration;
using Sayra.Client.Diagnostics.Events;
using Sayra.Client.Diagnostics.Interfaces;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;
using Sayra.Client.Diagnostics.Services;
using Xunit;

namespace Sayra.Client.Tests
{
    public class DiagnosticsTests
    {
        private readonly Mock<IWmiProvider> _mockWmi = new();
        private readonly Mock<IPerformanceCounterProvider> _mockPerf = new();
        private readonly Mock<IDisplayProvider> _mockDisplay = new();
        private readonly Mock<INetworkProvider> _mockNetwork = new();
        private readonly Mock<IGpuProvider> _mockGpu = new();
        private readonly Mock<ILogger<HardwareSpecificationService>> _loggerSpec = new();
        private readonly Mock<ILogger<HardwareTelemetryService>> _loggerTelemetry = new();
        private readonly Mock<ILogger<HardwareValidationService>> _loggerVal = new();
        private readonly Mock<ILogger<HardwareMonitoringService>> _loggerMonitor = new();

        private readonly DiagnosticsOptions _options = new()
        {
            PollingIntervalMs = 200,
            CacheDurationMinutes = 1,
            ValidationRetryCount = 3
        };

        private readonly IOptions<DiagnosticsOptions> _optionsAccessor;

        public DiagnosticsTests()
        {
            _optionsAccessor = Options.Create(_options);
        }

        private HardwareSpecification CreateMockSpec()
        {
            return new HardwareSpecification(
                new CpuInformation("AMD Ryzen 7", "AuthenticAMD", "X64", 16, 8, 16, 4.2),
                new List<GpuInformation> { new GpuInformation("NVIDIA RTX 4080", "NVIDIA", "551.23", 16L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024) },
                new MemoryInformation(32L * 1024 * 1024 * 1024, 32L * 1024 * 1024 * 1024, "DDR5", 6000),
                new MotherboardInformation("ASUS", "B650", "1.0"),
                new List<StorageInformation> { new StorageInformation("SSD", 1024L * 1024 * 1024 * 1024, 512L * 1024 * 1024 * 1024, "NTFS") },
                new List<DisplayInformation> { new DisplayInformation("1920x1080", 144.0, true, 96.0) },
                new OperatingSystemInformation("10.0", "19045", "Pro", "DirectX 12", true, "4.6"),
                new List<NetworkInformation> { new NetworkInformation("MyPC", "192.168.1.10", "fe80::1", "00:15:5d:01:23:45") }
            );
        }

        private HardwareMetrics CreateMockMetrics()
        {
            return new HardwareMetrics(
                CpuUsage: 12.5,
                GpuUsage: 25.0,
                RamUsage: 45.2,
                VramUsage: 15.0,
                DiskUsage: 2.1,
                NetworkUsage: 150.5,
                Uptime: TimeSpan.FromHours(2),
                ActiveProcesses: new List<string> { "explorer", "sayraclient" },
                RefreshRate: 144.0,
                FpsCapability: 144.0
            );
        }

        [Fact]
        public void Test1_CacheService_ShouldStoreAndExpireSuccessfully()
        {
            // Arrange
            var cacheService = new HardwareCacheService(_optionsAccessor);
            var spec = CreateMockSpec();

            // Act & Assert (Initial Empty State)
            Assert.Null(cacheService.Get());
            Assert.True(cacheService.IsExpired());

            // Act (Store)
            cacheService.Set(spec);

            // Assert (Retrieval)
            var retrieved = cacheService.Get();
            Assert.NotNull(retrieved);
            Assert.Equal("AMD Ryzen 7", retrieved!.Cpu.Name);
            Assert.False(cacheService.IsExpired());

            // Act (Clear)
            cacheService.Clear();

            // Assert (Cleared State)
            Assert.Null(cacheService.Get());
            Assert.True(cacheService.IsExpired());
        }

        [Fact]
        public async Task Test2_SpecificationService_ShouldLoadSpecsUsingProviders()
        {
            // Arrange
            _mockGpu.Setup(x => x.GetGpusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GpuInformation> { new GpuInformation("NVIDIA RTX 4080", "NVIDIA", "551.23", 16L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024) });
            _mockDisplay.Setup(x => x.GetDisplaysAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DisplayInformation> { new DisplayInformation("1920x1080", 144.0, true, 96.0) });
            _mockNetwork.Setup(x => x.GetNetworksAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NetworkInformation> { new NetworkInformation("Host", "192.168.1.10", "::1", "00:00:00:00") });

            var specService = new HardwareSpecificationService(
                _mockWmi.Object,
                _mockGpu.Object,
                _mockDisplay.Object,
                _mockNetwork.Object,
                _loggerSpec.Object
            );

            // Act
            var spec = await specService.GetSpecificationAsync();

            // Assert
            Assert.NotNull(spec);
            Assert.Equal("AMD Ryzen 7 7800X3D", spec.Cpu.Name); // Fallback logic default
            Assert.Single(spec.Gpus);
            Assert.Equal("NVIDIA RTX 4080", spec.Gpus[0].Name);
            Assert.Single(spec.Displays);
            Assert.Equal("1920x1080", spec.Displays[0].Resolution);
        }

        [Fact]
        public async Task Test3_TelemetryService_ShouldPollLiveMetrics()
        {
            // Arrange
            _mockPerf.Setup(x => x.GetCpuUsage()).Returns(15.2f);
            _mockPerf.Setup(x => x.GetRamUsage()).Returns(4096f);
            _mockPerf.Setup(x => x.GetDiskUsage()).Returns(1.2f);
            _mockGpu.Setup(x => x.GetGpuUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(25.5);
            _mockGpu.Setup(x => x.GetVramUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3.4);
            _mockNetwork.Setup(x => x.GetNetworkUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(150.4);

            var telemetryService = new HardwareTelemetryService(
                _mockPerf.Object,
                _mockGpu.Object,
                _mockNetwork.Object,
                _loggerTelemetry.Object
            );

            // Act
            var metrics = await telemetryService.GetLiveMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(15.2, metrics.CpuUsage, 1);
            Assert.Equal(4096.0, metrics.RamUsage, 1);
            Assert.Equal(25.5, metrics.GpuUsage, 1);
            Assert.Equal(3.4, metrics.VramUsage, 1);
            Assert.Equal(150.4, metrics.NetworkUsage, 1);
        }

        [Fact]
        public async Task Test4_ValidationService_ShouldDetectFailuresAndWarnings()
        {
            // Arrange
            var validationService = new HardwareValidationService(_loggerVal.Object);

            // 1. Healthy Specification
            var healthySpec = CreateMockSpec();
            var metrics = CreateMockMetrics();

            var result = await validationService.ValidateAsync(healthySpec, metrics);
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);

            // 2. Missing GPU Specification
            var missingGpuSpec = healthySpec with { Gpus = new List<GpuInformation>() };
            var resultMissingGpu = await validationService.ValidateAsync(missingGpuSpec, metrics);
            Assert.False(resultMissingGpu.IsValid);
            Assert.Contains(resultMissingGpu.Errors, e => e.Contains("Missing GPU"));

            // 3. Low/Missing RAM Specification
            var lowRamSpec = healthySpec with { Memory = new MemoryInformation(2L * 1024 * 1024 * 1024, 2L * 1024 * 1024 * 1024, "DDR4", 3200) };
            var resultLowRam = await validationService.ValidateAsync(lowRamSpec, metrics);
            Assert.True(resultLowRam.IsValid); // It's a warning, not an error
            Assert.Contains(resultLowRam.Warnings, w => w.Contains("Low system memory"));
        }

        [Fact]
        public async Task Test5_MonitoringService_ShouldRunAndPublishEvents()
        {
            // Arrange
            var spec = CreateMockSpec();
            var metrics = CreateMockMetrics();

            _mockGpu.Setup(x => x.GetGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(spec.Gpus);
            _mockDisplay.Setup(x => x.GetDisplaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(spec.Displays);
            _mockNetwork.Setup(x => x.GetNetworksAsync(It.IsAny<CancellationToken>())).ReturnsAsync(spec.Networks);

            var specService = new HardwareSpecificationService(_mockWmi.Object, _mockGpu.Object, _mockDisplay.Object, _mockNetwork.Object, _loggerSpec.Object);
            var cacheService = new HardwareCacheService(_optionsAccessor);

            var mockTelemetry = new Mock<IHardwareTelemetryService>();
            mockTelemetry.Setup(x => x.GetLiveMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(metrics);

            var mockValidation = new Mock<IHardwareValidationService>();
            mockValidation.Setup(x => x.ValidateAsync(It.IsAny<HardwareSpecification>(), It.IsAny<HardwareMetrics>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult(true, new List<string>(), new List<string>()));

            var monitorService = new HardwareMonitoringService(
                specService,
                mockTelemetry.Object,
                mockValidation.Object,
                cacheService,
                _optionsAccessor,
                _loggerMonitor.Object
            );

            bool initializedFired = false;
            bool metricsUpdatedFired = false;

            monitorService.HardwareInitialized += (s, e) =>
            {
                initializedFired = true;
                Assert.NotNull(e.Specification);
            };

            monitorService.HardwareMetricsUpdated += (s, e) =>
            {
                metricsUpdatedFired = true;
                Assert.NotNull(e.Metrics);
            };

            // Act
            using var cts = new CancellationTokenSource();
            var runTask = monitorService.StartMonitoringAsync(cts.Token);

            // Wait a short moment for initial poll to execute
            await Task.Delay(100);
            cts.Cancel();
            try { await runTask; } catch (OperationCanceledException) { }

            // Assert
            Assert.True(initializedFired);
            Assert.True(metricsUpdatedFired);
            Assert.NotNull(monitorService.CurrentSpecification);
            Assert.NotNull(monitorService.CurrentMetrics);
        }
    }
}
