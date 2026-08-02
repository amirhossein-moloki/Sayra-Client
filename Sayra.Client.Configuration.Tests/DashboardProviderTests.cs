using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Domain.Entities;
using Sayra.Client.Shared.Telemetry.Dashboard;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// Comprehensive test suite covering Stage 9 – Enterprise Dashboard Provider & Monitoring Integration.
    /// </summary>
    public class DashboardProviderTests
    {
        private readonly Mock<ILiveTelemetryService> _mockTelemetry = new();
        private readonly Mock<IPerformanceMonitor> _mockPerfMonitor = new();
        private readonly Mock<IAlertEngine> _mockAlertEngine = new();
        private readonly Mock<IHealthMonitor> _mockHealthMonitor = new();
        private readonly Mock<ISessionRepository> _mockSessionRepo = new();
        private readonly Mock<ISecurityHardeningService> _mockSecurityHardening = new();
        private readonly IOptions<DashboardOptions> _options;

        public DashboardProviderTests()
        {
            _options = Options.Create(new DashboardOptions
            {
                RefreshIntervalSeconds = 2,
                MaxVisibleAlerts = 10
            });
        }

        private DashboardProvider CreateProvider()
        {
            return new DashboardProvider(
                _mockTelemetry.Object,
                _mockPerfMonitor.Object,
                _mockAlertEngine.Object,
                _mockHealthMonitor.Object,
                _mockSessionRepo.Object,
                _mockSecurityHardening.Object,
                _options,
                NullLogger<DashboardProvider>.Instance
            );
        }

        [Fact]
        public async Task GetDashboardSnapshotAsync_Success_AggregatesDataFromExistingServices()
        {
            // Arrange
            var telemetryData = new LiveTelemetryData
            {
                CpuUsagePercent = 45.5,
                RamUsedMb = 2048,
                RamTotalMb = 8192,
                FreeSpaceGb = 120.4,
                PingMs = 12
            };
            _mockTelemetry.Setup(x => x.CaptureSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(telemetryData);

            var perfSnapshot = new PerformanceSnapshot
            {
                DatabaseLatency = TimeSpan.FromMilliseconds(5),
                IpcLatency = TimeSpan.FromMilliseconds(2),
                TcpLatency = TimeSpan.FromMilliseconds(15),
                DiskLatency = TimeSpan.FromMilliseconds(8),
                CacheHitRatio = 0.85,
                DownloadSpeed = 1024 * 500, // 500 KB/s
                QueueLength = 4,
                ThreadPoolThreads = 12,
                AsyncOperationsCount = 2,
                GarbageCollectionCount = 1
            };
            _mockPerfMonitor.Setup(x => x.GetLatestPerformanceSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(perfSnapshot);

            var activeAlerts = new List<AlertRecord>
            {
                new() { Name = "High Memory", Priority = AlertPriority.Warning, Subsystem = SubsystemType.Telemetry },
                new() { Name = "Tamper Detected", Priority = AlertPriority.Critical, Subsystem = SubsystemType.Security }
            };
            _mockAlertEngine.Setup(x => x.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(activeAlerts);

            var activeSessions = new List<RuntimeSession>
            {
                new() { UserId = "user1", GameId = "game_101" },
                new() { UserId = "user2", GameId = "" } // Online but no game
            };
            _mockSessionRepo.Setup(x => x.GetActiveSessionsAsync())
                .ReturnsAsync(activeSessions);

            var detailedHealth = new Dictionary<string, SubsystemHealthInfo>
            {
                { "Authentication", new SubsystemHealthInfo { SubsystemName = "Authentication", State = SubsystemHealthState.Healthy } },
                { "Database", new SubsystemHealthInfo { SubsystemName = "Database", State = SubsystemHealthState.Healthy, FailureCount = 1 } }
            };
            _mockHealthMonitor.Setup(x => x.GetDetailedHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(detailedHealth);
            _mockHealthMonitor.Setup(x => x.GetHealthSummaryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("System operational");

            _mockSecurityHardening.Setup(x => x.VerifySystemIntegrityAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockSecurityHardening.Setup(x => x.ValidatePolicyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SecurityValidationResult { TargetName = "policies", ValidationState = SecurityValidationState.Passed });

            var provider = CreateProvider();

            // Act
            var snapshot = await provider.GetDashboardSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot.LiveMachinesCount);
            Assert.Equal(2, snapshot.OnlineUsersCount);
            Assert.Equal(1, snapshot.RunningGamesCount);
            Assert.Equal(45.5, snapshot.CpuUsagePercent);
            Assert.Equal(25.0, snapshot.MemoryUsagePercent); // 2048/8192 * 100
            Assert.Equal(1, snapshot.FailuresCount); // 1 failure in Database
            Assert.Equal(2, snapshot.ActiveAlertsCount);
            Assert.Equal(500 * 1024, snapshot.DownloadsSpeedBytesPerSec);
            Assert.True(snapshot.NetworkConnected);
            Assert.Equal(100.0, snapshot.PolicyCompliancePercent);
            Assert.Equal("System operational", snapshot.RecoveryStatusSummary);
            Assert.Equal(1, snapshot.SecurityViolationsCount); // Security alerts count
        }

        [Fact]
        public async Task GetDashboardSnapshotAsync_FailureIsolation_FailingSubsystemDoesNotPreventSnapshot()
        {
            // Arrange
            // Set up one service to throw an exception
            _mockTelemetry.Setup(x => x.CaptureSnapshotAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Simulation Telemetry Failure"));

            var perfSnapshot = new PerformanceSnapshot { QueueLength = 3 };
            _mockPerfMonitor.Setup(x => x.GetLatestPerformanceSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(perfSnapshot);

            _mockAlertEngine.Setup(x => x.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AlertRecord>());

            _mockSessionRepo.Setup(x => x.GetActiveSessionsAsync())
                .ReturnsAsync(new List<RuntimeSession>());

            _mockHealthMonitor.Setup(x => x.GetDetailedHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, SubsystemHealthInfo>());
            _mockHealthMonitor.Setup(x => x.GetHealthSummaryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("Degraded");

            var provider = CreateProvider();

            // Act
            var snapshot = await provider.GetDashboardSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.Equal(0.0, snapshot.CpuUsagePercent); // Fallback to default
            Assert.Equal(0, snapshot.OnlineUsersCount);
            Assert.Equal("Degraded", snapshot.RecoveryStatusSummary);
            // Verify that the exception did not bubble up to fail the whole process
            _mockTelemetry.Verify(x => x.CaptureSnapshotAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SnapshotCaching_ServesCachedData_AvoidsMultipleRecomputations()
        {
            // Arrange
            _mockTelemetry.Setup(x => x.CaptureSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LiveTelemetryData { CpuUsagePercent = 10 });
            _mockPerfMonitor.Setup(x => x.GetLatestPerformanceSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PerformanceSnapshot());

            var provider = CreateProvider();

            // Act
            var snap1 = await provider.GetDashboardSnapshotAsync();
            var snap2 = await provider.GetDashboardSnapshotAsync();

            // Assert
            Assert.Same(snap1, snap2); // Confirms cached reference is served
            _mockTelemetry.Verify(x => x.CaptureSnapshotAsync(It.IsAny<CancellationToken>()), Times.Once); // Dependency called only once
        }

        [Fact]
        public async Task RefreshAsync_ForcesInvalidation_RebuildsCacheOnDemand()
        {
            // Arrange
            _mockTelemetry.Setup(x => x.CaptureSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LiveTelemetryData { CpuUsagePercent = 20 });
            _mockPerfMonitor.Setup(x => x.GetLatestPerformanceSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PerformanceSnapshot());

            var provider = CreateProvider();

            // Initial load
            var snap1 = await provider.GetDashboardSnapshotAsync();

            // Act - Manual Force Refresh
            await provider.RefreshAsync();

            // Assert
            _mockTelemetry.Verify(x => x.CaptureSnapshotAsync(It.IsAny<CancellationToken>()), Times.Exactly(2)); // Rebuilt
        }

        [Fact]
        public async Task StreamDashboardUpdatesAsync_YieldsSnapshots_UntilCancelled()
        {
            // Arrange
            _mockTelemetry.Setup(x => x.CaptureSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LiveTelemetryData { CpuUsagePercent = 50 });
            _mockPerfMonitor.Setup(x => x.GetLatestPerformanceSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PerformanceSnapshot());

            var provider = CreateProvider();
            using var cts = new CancellationTokenSource();

            var receivedSnapshots = new List<DashboardSnapshot>();

            // Act
            var streamTask = provider.StreamDashboardUpdatesAsync(snap =>
            {
                receivedSnapshots.Add(snap);
                if (receivedSnapshots.Count >= 2)
                {
                    cts.Cancel(); // Cancel after receiving two updates
                }
            }, cts.Token);

            try
            {
                await streamTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Assert
            Assert.True(receivedSnapshots.Count >= 2);
            Assert.Equal(50, receivedSnapshots[0].CpuUsagePercent);
        }

        [Fact]
        public async Task ReadModels_ProvideOptimizedReadLayers()
        {
            // Arrange
            _mockTelemetry.Setup(x => x.CaptureSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LiveTelemetryData { CpuUsagePercent = 30 });
            _mockPerfMonitor.Setup(x => x.GetLatestPerformanceSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PerformanceSnapshot { CacheHitRatio = 0.95 });

            var provider = CreateProvider();

            // Act
            var overview = await provider.GetOverviewAsync();
            var status = await provider.GetSubsystemStatusAsync();
            var perf = await provider.GetPerformanceSummaryAsync();
            var alert = await provider.GetAlertSummaryAsync();
            var security = await provider.GetSecuritySummaryAsync();
            var recovery = await provider.GetRecoverySummaryAsync();
            var compliance = await provider.GetComplianceSummaryAsync();

            // Assert
            Assert.NotNull(overview);
            Assert.NotNull(status);
            Assert.NotNull(perf);
            Assert.NotNull(alert);
            Assert.NotNull(security);
            Assert.NotNull(recovery);
            Assert.NotNull(compliance);

            Assert.Equal(30.0, perf.CpuUsagePercent);
            Assert.Equal(0.95, perf.CacheHitRatio);
            Assert.Equal("Healthy", status.Authentication.Health); // Fallback default
        }

        [Fact]
        public async Task GetDashboardSnapshotAsync_PropagatesCancellation()
        {
            // Arrange
            var provider = CreateProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await provider.GetDashboardSnapshotAsync(cts.Token));
        }

        [Fact]
        public void DependencyInjection_CanSuccessfullyResolveDashboardProvider()
        {
            // Arrange
            var services = new ServiceCollection();
            var configData = new Dictionary<string, string>
            {
                { "Observability:Dashboard:RefreshIntervalSeconds", "5" },
                { "Observability:Dashboard:MaxVisibleAlerts", "100" }
            };

            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Add required mocks to service collection
            services.AddSingleton(_mockTelemetry.Object);
            services.AddSingleton(_mockPerfMonitor.Object);
            services.AddSingleton(_mockAlertEngine.Object);
            services.AddSingleton(_mockHealthMonitor.Object);
            services.AddSingleton(_mockSessionRepo.Object);
            services.AddSingleton(_mockSecurityHardening.Object);
            services.AddLogging();

            // Register Observability services (this includes our new DashboardProvider)
            services.AddObservabilityServices(configuration);

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var resolvedProvider = serviceProvider.GetService<IDashboardProvider>();

            // Assert
            Assert.NotNull(resolvedProvider);
            Assert.IsType<DashboardProvider>(resolvedProvider);
        }
    }
}
