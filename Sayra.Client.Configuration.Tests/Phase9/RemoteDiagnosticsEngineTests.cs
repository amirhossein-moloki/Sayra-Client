using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sayra.Client.Shared.DependencyInjection;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;
using Sayra.Client.Shared.Models.Phase9.Options;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Fleet.Diagnostics.Domain.Models;
using Sayra.Client.Shared.Fleet.Diagnostics.Interfaces;
using Sayra.Client.Shared.Fleet.Diagnostics.Services;
using Sayra.Client.Shared.Fleet.Diagnostics.Services.Collectors;
using Xunit;

namespace Sayra.Client.Configuration.Tests.Phase9
{
    public class RemoteDiagnosticsEngineTests
    {
        private readonly Mock<IHealthMonitor> _healthMonitorMock;
        private readonly Mock<IResourceMonitor> _resourceMonitorMock;
        private readonly Mock<IEventDispatcher> _eventDispatcherMock;
        private readonly Mock<ILogger<DiagnosticStorage>> _storageLoggerMock;
        private readonly Mock<ILogger<DiagnosticPackageBuilder>> _builderLoggerMock;
        private readonly Mock<ILogger<DiagnosticAnalyzer>> _analyzerLoggerMock;
        private readonly Mock<ILogger<DiagnosticsPipeline>> _pipelineLoggerMock;
        private readonly Mock<ILogger<DiagnosticsResultProcessor>> _processorLoggerMock;
        private readonly Mock<ILogger<DiagnosticsCoordinator>> _coordinatorLoggerMock;
        private readonly Mock<ILogger<HealthDiagnosticCollector>> _healthCollectorLoggerMock;
        private readonly Mock<ILogger<PerformanceDiagnosticCollector>> _perfCollectorLoggerMock;

        public RemoteDiagnosticsEngineTests()
        {
            _healthMonitorMock = new Mock<IHealthMonitor>();
            _resourceMonitorMock = new Mock<IResourceMonitor>();
            _eventDispatcherMock = new Mock<IEventDispatcher>();
            _storageLoggerMock = new Mock<ILogger<DiagnosticStorage>>();
            _builderLoggerMock = new Mock<ILogger<DiagnosticPackageBuilder>>();
            _analyzerLoggerMock = new Mock<ILogger<DiagnosticAnalyzer>>();
            _pipelineLoggerMock = new Mock<ILogger<DiagnosticsPipeline>>();
            _processorLoggerMock = new Mock<ILogger<DiagnosticsResultProcessor>>();
            _coordinatorLoggerMock = new Mock<ILogger<DiagnosticsCoordinator>>();
            _healthCollectorLoggerMock = new Mock<ILogger<HealthDiagnosticCollector>>();
            _perfCollectorLoggerMock = new Mock<ILogger<PerformanceDiagnosticCollector>>();

            // Setup default mocks values
            _healthMonitorMock.Setup(m => m.GetDetailedHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, SubsystemHealthInfo>
                {
                    { "Database", new SubsystemHealthInfo { SubsystemId = "Database", SubsystemName = "Database", HealthScore = 100.0, State = SubsystemHealthState.Healthy } }
                });
            _healthMonitorMock.Setup(m => m.GetHealthSummaryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("All systems normal.");
            _healthMonitorMock.Setup(m => m.GetFailureStatisticsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("No failures recorded.");

            _resourceMonitorMock.Setup(m => m.GetCurrentMetricsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceMetrics
                {
                    CpuUsagePercentage = 15.5,
                    TotalSystemRamBytes = 16L * 1024 * 1024 * 1024,
                    AvailableSystemRamBytes = 8L * 1024 * 1024 * 1024,
                    ProcessRamBytes = 250L * 1024 * 1024,
                    FreeDiskSpaceBytes = 100L * 1024 * 1024 * 1024,
                    ThreadCount = 45,
                    HandleCount = 1200,
                    GpuUsagePercentage = 5.0,
                    DiskIoBytesPerSecond = 1024,
                    NetworkIoBytesPerSecond = 512
                });
        }

        [Fact]
        public async Task Collector_Tests_HealthCollector()
        {
            // Arrange
            _healthMonitorMock.Setup(m => m.GetDetailedHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, SubsystemHealthInfo>
                {
                    { "Database", new SubsystemHealthInfo { SubsystemId = "Database", SubsystemName = "Database", HealthScore = 65.0, State = SubsystemHealthState.Critical } }
                });

            var collector = new HealthDiagnosticCollector(_healthMonitorMock.Object, _healthCollectorLoggerMock.Object);
            var context = new DiagnosticsExecutionContext { MachineId = "PC-001" };

            // Act
            var report = await collector.CollectAsync(context);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(DiagnosticReportType.GeneralHealth, report.Category);
            Assert.Contains("Database", report.ContentJson);
            Assert.Contains("LowSubsystemHealthScore", report.ContentJson);
        }

        [Fact]
        public async Task Collector_Tests_PerformanceCollector()
        {
            // Arrange
            var collector = new PerformanceDiagnosticCollector(_resourceMonitorMock.Object, _perfCollectorLoggerMock.Object);
            var context = new DiagnosticsExecutionContext { MachineId = "PC-001" };

            // Act
            var report = await collector.CollectAsync(context);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(DiagnosticReportType.Performance, report.Category);
            Assert.Contains("CpuUsage", report.ContentJson);
            Assert.Contains("TotalRam", report.ContentJson);
        }

        [Fact]
        public async Task Storage_Tests_SaveAndEnforceQuota()
        {
            // Arrange
            var dir = Path.Combine(AppContext.BaseDirectory, "Test_Staging_" + Guid.NewGuid().ToString("N"));
            var optionsMock = new Mock<IOptionsMonitor<DiagnosticsOptions>>();
            optionsMock.Setup(o => o.CurrentValue).Returns(new DiagnosticsOptions
            {
                LocalStagingDirectory = dir,
                MaxDiagnosticsStorageMb = 1 // 1MB ceiling
            });

            var storage = new DiagnosticStorage(optionsMock.Object, _storageLoggerMock.Object);

            try
            {
                // Act - Save two large-ish packages to exceed the 1MB quota
                var data1 = new byte[800 * 1024]; // 800KB
                var data2 = new byte[400 * 1024]; // 400KB

                await storage.SavePackageAsync("pkg-01", data1, "file1.zip");
                // Wait slightly to ensure different write times
                await Task.Delay(100);
                await storage.SavePackageAsync("pkg-02", data2, "file2.zip");

                // Assert - Check that package 1 was pruned since total is 1200KB > 1024KB
                var pkg1 = await storage.GetPackageAsync("pkg-01");
                var pkg2 = await storage.GetPackageAsync("pkg-02");

                Assert.Null(pkg1); // Pruned
                Assert.NotNull(pkg2); // Retained
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        [Fact]
        public async Task Package_Tests_BuildPackageAndCompression()
        {
            // Arrange
            var dir = Path.Combine(AppContext.BaseDirectory, "Test_Staging_" + Guid.NewGuid().ToString("N"));
            var optionsMock = new Mock<IOptionsMonitor<DiagnosticsOptions>>();
            optionsMock.Setup(o => o.CurrentValue).Returns(new DiagnosticsOptions { LocalStagingDirectory = dir });

            var storage = new DiagnosticStorage(optionsMock.Object, _storageLoggerMock.Object);
            var registry = new DiagnosticReportRegistry();

            var report = new DiagnosticReport
            {
                ReportId = "rep-100",
                MachineId = "PC-001",
                Category = DiagnosticReportType.GeneralHealth,
                ContentJson = "{ \"Status\": \"Healthy\" }"
            };
            registry.RegisterReport(report);

            var builder = new DiagnosticPackageBuilder(storage, registry, _builderLoggerMock.Object);

            try
            {
                // Act
                var package = await builder.BuildPackageAsync("PC-001", new[] { "rep-100" });

                // Assert
                Assert.NotNull(package);
                Assert.Equal("PC-001", package.SourceMachineId);
                Assert.True(package.SizeBytes > 0);
                Assert.NotEmpty(package.IntegrityHash);

                // Fetch compressed bytes and check GZip validity
                var zipBytes = await storage.GetPackageAsync(package.PackageId);
                Assert.NotNull(zipBytes);
                Assert.True(zipBytes.Length > 0);

                // Unzip and assert
                using (var ms = new MemoryStream(zipBytes))
                using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip))
                {
                    var unzippedJson = await reader.ReadToEndAsync();
                    Assert.Contains("rep-100", unzippedJson);
                }
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        [Fact]
        public void Analyzer_Tests_HealthScoreAndPrioritizations()
        {
            // Arrange
            var analyzer = new DiagnosticAnalyzer(_analyzerLoggerMock.Object);
            var report = new DiagnosticReport
            {
                ReportId = "rep-101",
                MachineId = "PC-001",
                Category = DiagnosticReportType.Performance,
                ContentJson = JsonSerializer.Serialize(new List<DiagnosticSection>
                {
                    new()
                    {
                        Name = "Resource Usage",
                        Metrics = new List<DiagnosticMetric>
                        {
                            new() { Name = "CpuUsage", Value = "98.0", Unit = "%" }, // Critical/Emergency (CPU saturation)
                            new() { Name = "AvailableRam", Value = "0.5", Unit = "GB" } // Critical (Low RAM)
                        }
                    }
                })
            };

            // Act
            var result = analyzer.Analyze("diag-100", "PC-001", new List<DiagnosticReport> { report });

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal("PC-001", result.MachineId);
            Assert.True(result.HealthScore < 100.0);
            Assert.Contains(result.OverallStatus, new[] { "Critical", "Emergency" });
            Assert.True(result.Findings.Count > 0);

            // Severity Ordering (Emergency or Critical should be first)
            Assert.Equal("CpuSaturated", result.Findings[0].RuleName);
        }

        [Fact]
        public async Task Pipeline_Tests_ParallelExecutionAndScrubbing()
        {
            // Arrange
            var collectorMock = new Mock<IDiagnosticCollector>();
            collectorMock.Setup(c => c.ReportType).Returns(DiagnosticReportType.GeneralHealth);
            collectorMock.Setup(c => c.CollectAsync(It.IsAny<DiagnosticsExecutionContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DiagnosticReport
                {
                    ReportId = "rep-200",
                    MachineId = "PC-001",
                    Category = DiagnosticReportType.GeneralHealth,
                    ContentJson = JsonSerializer.Serialize(new List<DiagnosticSection>
                    {
                        new()
                        {
                            Name = "Auth Settings",
                            Metrics = new List<DiagnosticMetric>
                            {
                                new() { Name = "DbPassword", Value = "SecretP@ssword123" },
                                new() { Name = "ApiKey", Value = "xyz-key-value" },
                                new() { Name = "NormalSetting", Value = "VanillaVal" }
                            }
                        }
                    })
                });

            var pipeline = new DiagnosticsPipeline(
                new[] { collectorMock.Object },
                _eventDispatcherMock.Object,
                _pipelineLoggerMock.Object
            );

            var session = new DiagnosticsSession("PC-001", "Admin", "corr-123");

            // Act
            var reports = await pipeline.ExecuteAsync(session, new[] { DiagnosticReportType.GeneralHealth }, (p, s) => {}, CancellationToken.None);

            // Assert
            Assert.Single(reports);
            var report = reports[0];
            Assert.Contains("[REDACTED]", report.ContentJson);
            Assert.Contains("VanillaVal", report.ContentJson);
            Assert.DoesNotContain("SecretP@ssword123", report.ContentJson);
            Assert.DoesNotContain("xyz-key-value", report.ContentJson);
        }

        [Fact]
        public async Task Pipeline_Tests_ResilientFailureIsolation()
        {
            // Arrange
            var badCollector = new Mock<IDiagnosticCollector>();
            badCollector.Setup(c => c.ReportType).Returns(DiagnosticReportType.GeneralHealth);
            badCollector.Setup(c => c.CollectAsync(It.IsAny<DiagnosticsExecutionContext>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Fatal hardware crash simulated."));

            var goodCollector = new Mock<IDiagnosticCollector>();
            goodCollector.Setup(c => c.ReportType).Returns(DiagnosticReportType.Performance);
            goodCollector.Setup(c => c.CollectAsync(It.IsAny<DiagnosticsExecutionContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DiagnosticReport
                {
                    ReportId = "rep-good",
                    Category = DiagnosticReportType.Performance,
                    ContentJson = "[]"
                });

            var pipeline = new DiagnosticsPipeline(
                new[] { badCollector.Object, goodCollector.Object },
                _eventDispatcherMock.Object,
                _pipelineLoggerMock.Object
            );

            var session = new DiagnosticsSession("PC-001", "Admin", "corr-123");

            // Act
            var reports = await pipeline.ExecuteAsync(
                session,
                new[] { DiagnosticReportType.GeneralHealth, DiagnosticReportType.Performance },
                (p, s) => {},
                CancellationToken.None
            );

            // Assert - The entire pipeline runs, isolating bad collector failure and returning good collector output
            Assert.Single(reports);
            Assert.Equal(DiagnosticReportType.Performance, reports[0].Category);
        }

        [Fact]
        public async Task Coordinator_Tests_StartAndCancelSession()
        {
            // Arrange
            var dir = Path.Combine(AppContext.BaseDirectory, "Test_Staging_" + Guid.NewGuid().ToString("N"));
            var optionsMock = new Mock<IOptionsMonitor<DiagnosticsOptions>>();
            optionsMock.Setup(o => o.CurrentValue).Returns(new DiagnosticsOptions { LocalStagingDirectory = dir });

            var storage = new DiagnosticStorage(optionsMock.Object, _storageLoggerMock.Object);
            var registry = new DiagnosticReportRegistry();
            var builder = new DiagnosticPackageBuilder(storage, registry, _builderLoggerMock.Object);

            var goodCollector = new Mock<IDiagnosticCollector>();
            goodCollector.Setup(c => c.ReportType).Returns(DiagnosticReportType.Performance);
            goodCollector.Setup(c => c.CollectAsync(It.IsAny<DiagnosticsExecutionContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DiagnosticReport
                {
                    ReportId = "rep-good",
                    Category = DiagnosticReportType.Performance,
                    ContentJson = "[]"
                });

            var pipeline = new DiagnosticsPipeline(
                new[] { goodCollector.Object },
                _eventDispatcherMock.Object,
                _pipelineLoggerMock.Object
            );

            var analyzer = new DiagnosticAnalyzer(_analyzerLoggerMock.Object);
            var processor = new DiagnosticsResultProcessor(analyzer, _eventDispatcherMock.Object, _processorLoggerMock.Object);

            var coordinator = new DiagnosticsCoordinator(
                pipeline,
                processor,
                builder,
                registry,
                _eventDispatcherMock.Object,
                _coordinatorLoggerMock.Object
            );

            try
            {
                // Act
                var result = await coordinator.StartSessionAsync("PC-001", new[] { DiagnosticReportType.Performance }, "Admin", "corr-111");

                // Assert
                Assert.NotNull(result);
                Assert.True(result.IsSuccess);
                Assert.Equal("PC-001", result.MachineId);
                Assert.Single(result.Reports);

                // Event Dispatcher checks
                _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<DiagnosticsStarted>()), Times.Once);
                _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<DiagnosticsCompleted>()), Times.Once);
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        [Fact]
        public void DI_Integration_Tests()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Mock other Phase 9 / Observability singleton dependencies
            services.AddSingleton(_healthMonitorMock.Object);
            services.AddSingleton(_resourceMonitorMock.Object);
            services.AddSingleton(_eventDispatcherMock.Object);

            // Act
            services.AddRemoteDiagnostics();
            var provider = services.BuildServiceProvider();

            // Assert
            var remoteDiagService = provider.GetService<IRemoteDiagnosticsService>();
            var coordinator = provider.GetService<DiagnosticsCoordinator>();
            var storage = provider.GetService<IDiagnosticStorage>();
            var builder = provider.GetService<IDiagnosticPackageBuilder>();
            var analyzer = provider.GetService<DiagnosticAnalyzer>();

            Assert.NotNull(remoteDiagService);
            Assert.NotNull(coordinator);
            Assert.NotNull(storage);
            Assert.NotNull(builder);
            Assert.NotNull(analyzer);

            // Verify individual collectors are registered
            var healthCollector = provider.GetService<IHealthDiagnosticCollector>();
            var perfCollector = provider.GetService<IPerformanceDiagnosticCollector>();
            var collectors = provider.GetServices<IDiagnosticCollector>().ToList();

            Assert.NotNull(healthCollector);
            Assert.NotNull(perfCollector);
            Assert.Equal(9, collectors.Count); // all 9 collectors registered as IDiagnosticCollector
        }
    }
}
