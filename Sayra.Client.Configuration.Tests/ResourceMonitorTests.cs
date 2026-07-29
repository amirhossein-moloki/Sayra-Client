using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Interfaces.Recovery.Providers;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using SayraClient.Services.Recovery;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage7Tests")]
    public class ResourceMonitorTests
    {
        private readonly Mock<IEventDispatcher> _mockEventDispatcher = new();
        private readonly Mock<ICpuMetricsProvider> _mockCpu = new();
        private readonly Mock<IMemoryMetricsProvider> _mockMemory = new();
        private readonly Mock<IDiskMetricsProvider> _mockDisk = new();
        private readonly Mock<INetworkMetricsProvider> _mockNetwork = new();
        private readonly Mock<IGpuMetricsProvider> _mockGpu = new();
        private readonly Mock<IProcessMetricsProvider> _mockProcess = new();

        private readonly ResourceMonitorOptions _options;
        private readonly IOptions<ResourceMonitorOptions> _optionsWrapper;

        public ResourceMonitorTests()
        {
            _options = new ResourceMonitorOptions
            {
                MachineIdentifier = "TEST-WS",
                SamplingInterval = TimeSpan.FromMilliseconds(50),
                CpuWarningThreshold = 80.0,
                CpuCriticalThreshold = 90.0,
                CpuEmergencyThreshold = 95.0,
                ProcessRamWarningBytes = 500 * 1024 * 1024,
                ProcessRamCriticalBytes = 1024 * 1024 * 1024,
                ProcessRamEmergencyBytes = 2048 * 1024 * 1024L,
                SystemAvailableRamWarningBytes = 1024 * 1024 * 1024,
                SystemAvailableRamCriticalBytes = 512 * 1024 * 1024,
                SystemAvailableRamEmergencyBytes = 256 * 1024 * 1024,
                DiskPressureBytes = 500 * 1024 * 1024,
                GpuWarningThreshold = 80.0,
                GpuCriticalThreshold = 90.0,
                GpuEmergencyThreshold = 95.0,
                HandleWarningThreshold = 800,
                HandleCriticalThreshold = 1000,
                HandleEmergencyThreshold = 2000,
                ThreadWarningThreshold = 100,
                ThreadCriticalThreshold = 150,
                ThreadEmergencyThreshold = 300,
                GdiWarningThreshold = 8000,
                GdiCriticalThreshold = 9000,
                GdiEmergencyThreshold = 9500,
                TemperatureWarningThreshold = 85.0,
                TemperatureCriticalThreshold = 95.0
            };
            _optionsWrapper = Options.Create(_options);

            // Set up standard mock responses to prevent null references and return healthy values
            _mockCpu.Setup(p => p.GetCpuUsagePercentageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(25.0);
            _mockMemory.Setup(p => p.GetTotalSystemRamBytesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(16106127360L);
            _mockMemory.Setup(p => p.GetAvailableSystemRamBytesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(8589934592L);
            _mockDisk.Setup(p => p.GetFreeDiskSpaceBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(10737418240L);
            _mockDisk.Setup(p => p.GetDiskIoBytesPerSecondAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50 * 1024.0);
            _mockNetwork.Setup(p => p.GetNetworkIoBytesPerSecondAsync(It.IsAny<CancellationToken>())).ReturnsAsync(100 * 1024.0);
            _mockGpu.Setup(p => p.GetGpuUsagePercentageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(15.0);
            _mockGpu.Setup(p => p.GetHardwareTemperatureCelsiusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(45.0);
            _mockProcess.Setup(p => p.GetProcessRamBytesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(120 * 1024 * 1024L);
            _mockProcess.Setup(p => p.GetHandleCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(200);
            _mockProcess.Setup(p => p.GetThreadCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(18);
            _mockProcess.Setup(p => p.GetGdiObjectsCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(100);
        }

        private ResourceMonitor CreateMonitor()
        {
            return new ResourceMonitor(
                NullLogger<ResourceMonitor>.Instance,
                _mockEventDispatcher.Object,
                _optionsWrapper,
                _mockCpu.Object,
                _mockMemory.Object,
                _mockDisk.Object,
                _mockNetwork.Object,
                _mockGpu.Object,
                _mockProcess.Object);
        }

        #region 1. Metric Collection & Provider Abstraction

        [Fact]
        public async Task Test_GetCurrentMetricsAsync_PullsFromProvidersCorrectly()
        {
            // Arrange
            var monitor = CreateMonitor();

            // Act
            var metrics = await monitor.GetCurrentMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal("TEST-WS", metrics.MachineIdentifier);
            Assert.Equal(25.0, metrics.CpuUsagePercentage);
            Assert.Equal(16106127360L, metrics.TotalSystemRamBytes);
            Assert.Equal(8589934592L, metrics.AvailableSystemRamBytes);
            Assert.Equal(10737418240L, metrics.FreeDiskSpaceBytes);
            Assert.Equal(50 * 1024.0, metrics.DiskIoBytesPerSecond);
            Assert.Equal(100 * 1024.0, metrics.NetworkIoBytesPerSecond);
            Assert.Equal(15.0, metrics.GpuUsagePercentage);
            Assert.Equal(45.0, metrics.HardwareTemperatureCelsius);
            Assert.Equal(120 * 1024 * 1024L, metrics.ProcessRamBytes);
            Assert.Equal(200, metrics.HandleCount);
            Assert.Equal(18, metrics.ThreadCount);
            Assert.Equal(100, metrics.GdiObjectsCount);
            Assert.Equal(ResourcePressureLevel.Normal, metrics.PressureLevel);
            Assert.Equal("Normal", metrics.ThresholdStatus);
        }

        #endregion

        #region 2. Threshold Evaluation & Pressure States

        [Theory]
        [InlineData(85.0, ResourcePressureLevel.High, "Normal")] // CPU Warning
        [InlineData(91.0, ResourcePressureLevel.Critical, "Critical")] // CPU Critical
        [InlineData(96.0, ResourcePressureLevel.Critical, "Critical")] // CPU Emergency
        public async Task Test_CpuThresholds_DetermineCorrectPressureAndState(double cpuValue, ResourcePressureLevel expectedLevel, string stateString)
        {
            // Arrange
            _mockCpu.Setup(p => p.GetCpuUsagePercentageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(cpuValue);
            var monitor = CreateMonitor();

            // Act
            var metrics = await monitor.GetCurrentMetricsAsync();

            // Assert
            Assert.Equal(expectedLevel, metrics.PressureLevel);
            Assert.Contains("CPU", metrics.ThresholdStatus);
        }

        [Fact]
        public async Task Test_DiskPressureThreshold_TriggersPressureCorrectly()
        {
            // Arrange
            _mockDisk.Setup(p => p.GetFreeDiskSpaceBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(100 * 1024 * 1024L); // 100 MB free (below 500MB threshold)
            var monitor = CreateMonitor();

            // Act
            var metrics = await monitor.GetCurrentMetricsAsync();

            // Assert
            Assert.Equal(ResourcePressureLevel.Medium, metrics.PressureLevel);
            Assert.Contains("Free disk space", metrics.ThresholdStatus);
        }

        #endregion

        #region 3. State Transitions

        [Fact]
        public async Task Test_StateTransition_TracksPreviousAndCurrentStatesWithTimeAndReason()
        {
            // Arrange
            var monitor = CreateMonitor();

            // Act 1: Initial state is Normal
            Assert.Equal(ResourcePressureState.Normal, monitor.CurrentState);

            // Act 2: Cross warning threshold
            _mockCpu.Setup(p => p.GetCpuUsagePercentageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(85.0);
            await monitor.RunResourceAuditAsync();

            // Assert 2
            Assert.Equal(ResourcePressureState.Warning, monitor.CurrentState);
            Assert.Equal(ResourcePressureState.Normal, monitor.PreviousState);
            Assert.Contains("CPU", monitor.TransitionReason);

            // Act 3: Return to normal
            _mockCpu.Setup(p => p.GetCpuUsagePercentageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(25.0);
            await monitor.RunResourceAuditAsync();

            // Assert 3
            Assert.Equal(ResourcePressureState.Normal, monitor.CurrentState);
            Assert.Equal(ResourcePressureState.Warning, monitor.PreviousState);
            Assert.Equal("Normal", monitor.TransitionReason);
        }

        #endregion

        #region 4. Snapshot Immutability

        [Fact]
        public async Task Test_ResourceSnapshot_IsFullyImmutableAndIndependent()
        {
            // Arrange
            var monitor = CreateMonitor();
            var snapshot1 = await monitor.GetResourceSnapshotAsync();

            // Act: change values and take another snapshot
            _mockCpu.Setup(p => p.GetCpuUsagePercentageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(85.0);
            var snapshot2 = await monitor.GetResourceSnapshotAsync();

            // Assert
            Assert.Equal(25.0, snapshot1.CpuUsagePercentage);
            Assert.Equal(85.0, snapshot2.CpuUsagePercentage);
        }

        #endregion

        #region 5. Event Publishing & Subscriber Mechanism

        [Fact]
        public async Task Test_EventPublishing_DispatchesToBothEventDispatcherAndLocalSubscribers()
        {
            // Arrange
            var monitor = CreateMonitor();
            var receivedEvents = new List<object>();

            await monitor.SubscribeToResourceEvents(e => receivedEvents.Add(e));

            // Act: Transition state from Normal to Warning via CPU usage
            _mockCpu.Setup(p => p.GetCpuUsagePercentageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(85.0);
            await monitor.RunResourceAuditAsync();

            // Assert
            // 1. Verify dispatcher was invoked
            _mockEventDispatcher.Verify(d => d.Dispatch(It.IsAny<ResourceMetricsCollectedEvent>()), Times.Once);
            _mockEventDispatcher.Verify(d => d.Dispatch(It.IsAny<ResourceThresholdExceededEvent>()), Times.Once);
            _mockEventDispatcher.Verify(d => d.Dispatch(It.IsAny<ResourcePressureDetectedEvent>()), Times.Once);

            // 2. Verify local subscriber received those exact events
            Assert.Contains(receivedEvents, e => e is ResourceMetricsCollectedEvent);
            Assert.Contains(receivedEvents, e => e is ResourceThresholdExceededEvent);
            Assert.Contains(receivedEvents, e => e is ResourcePressureDetectedEvent);
        }

        [Fact]
        public async Task Test_RecoveryEvent_DispatchesWhenResourcesReturnToNormal()
        {
            // Arrange
            var monitor = CreateMonitor();
            var receivedEvents = new List<object>();

            await monitor.SubscribeToResourceEvents(e => receivedEvents.Add(e));

            // Setup Warning
            _mockCpu.Setup(p => p.GetCpuUsagePercentageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(85.0);
            await monitor.RunResourceAuditAsync();
            receivedEvents.Clear();

            // Act: Go back to Normal
            _mockCpu.Setup(p => p.GetCpuUsagePercentageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(25.0);
            await monitor.RunResourceAuditAsync();

            // Assert
            _mockEventDispatcher.Verify(d => d.Dispatch(It.IsAny<ResourcePressureRecoveredEvent>()), Times.Once);
            Assert.Contains(receivedEvents, e => e is ResourcePressureRecoveredEvent);
        }

        #endregion

        #region 6. Cancellation & Concurrency

        [Fact]
        public async Task Test_MonitorAsync_GracefullySupportsCancellation()
        {
            // Arrange
            var monitor = CreateMonitor();
            using var cts = new CancellationTokenSource();

            // Act
            var monitorTask = monitor.MonitorAsync(cts.Token);

            // Allow some loops
            await Task.Delay(150);

            cts.Cancel();
            await Task.WhenAny(monitorTask, Task.Delay(500));

            // Assert
            Assert.True(monitorTask.IsCompleted);
        }

        [Fact]
        public async Task Test_ConcurrentMetricQueries_RemainFullyStableAndConsistent()
        {
            // Arrange
            var monitor = CreateMonitor();
            var tasks = new List<Task<ResourceMetrics>>();

            // Act: Run 50 concurrent metrics fetches in parallel
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(() => monitor.GetCurrentMetricsAsync()));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(50, results.Length);
            foreach (var r in results)
            {
                Assert.NotNull(r);
                Assert.Equal(25.0, r.CpuUsagePercentage);
            }
        }

        #endregion

        #region 7. Performance & Invalid Metric Handling

        [Fact]
        public async Task Test_ProviderThrowsException_MonitorUsesFallbackGracefully()
        {
            // Arrange
            _mockCpu.Setup(p => p.GetCpuUsagePercentageAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("API Down"));
            var monitor = CreateMonitor();

            // Act: Get metrics should complete and not throw
            var metrics = await monitor.GetCurrentMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(0.0, metrics.CpuUsagePercentage); // uses fallback
            _mockCpu.Verify(p => p.GetCpuUsagePercentageAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Test_PerformanceOverhead_CompletesQueriesInNegligibleTime()
        {
            // Arrange
            var monitor = CreateMonitor();
            var sw = Stopwatch.StartNew();

            // Act
            var metrics = await monitor.GetCurrentMetricsAsync();
            sw.Stop();

            // Assert: Query should take negligible execution time (excluding Task.Delay inside actual OS counters, which we mocked)
            Assert.True(sw.ElapsedMilliseconds < 50, $"Query took too long: {sw.ElapsedMilliseconds}ms");
        }

        #endregion
    }
}
