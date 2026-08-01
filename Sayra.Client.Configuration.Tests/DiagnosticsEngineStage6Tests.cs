using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Telemetry.Diagnostics;
using Sayra.Client.Shared.Telemetry.Diagnostics.Modules;
using Sayra.Client.Shared.Telemetry.Performance;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    public class DiagnosticsEngineStage6Tests
    {
        private readonly Mock<ILogger<DiagnosticsEngine>> _loggerMock = new();
        private readonly Mock<IResourceMonitor> _resourceMonitorMock = new();
        private readonly Mock<IHardwareSensorProvider> _sensorProviderMock = new();
        private readonly Mock<ISecurityHardeningService> _securityServiceMock = new();
        private readonly Mock<IServiceProvider> _serviceProviderMock = new();
        private readonly Mock<IPerformanceMonitor> _performanceMonitorMock = new();

        public DiagnosticsEngineStage6Tests()
        {
            // Setup service provider mock to support reflection-based resolves in modules
            _serviceProviderMock.Setup(sp => sp.GetService(It.IsAny<Type>())).Returns((object?)null);

            // Setup standard performance snapshot mocks
            _performanceMonitorMock.Setup(p => p.GetLatestPerformanceSnapshotAsync(It.IsAny<CancellationToken>()))
                                   .ReturnsAsync(new PerformanceSnapshot
                                   {
                                       DatabaseLatency = TimeSpan.FromMilliseconds(2.5),
                                       TcpLatency = TimeSpan.FromMilliseconds(15.0),
                                       IpcLatency = TimeSpan.FromMilliseconds(1.2),
                                       DownloadSpeed = 45.0 * 1024 * 1024
                                   });
        }

        [Fact]
        public async Task Test_HardwareDiagnosticModule_Evaluates_AllMetrics()
        {
            // Arrange
            var metrics = new ResourceMetrics
            {
                CpuUsagePercentage = 95.0, // High CPU
                AvailableSystemRamBytes = 256 * 1024 * 1024, // Low RAM (< 512MB)
                TotalSystemRamBytes = 8 * 1024 * 1024 * 1024L,
                FreeDiskSpaceBytes = 5 * 1024 * 1024 * 1024L, // Low disk (< 10GB)
                GpuUsagePercentage = 10.0
            };

            _resourceMonitorMock.Setup(r => r.GetCurrentMetricsAsync(It.IsAny<CancellationToken>()))
                                .ReturnsAsync(metrics);

            _sensorProviderMock.Setup(s => s.GetCpuTemperature()).Returns(90.0); // High temp
            _sensorProviderMock.Setup(s => s.GetGpuTemperature()).Returns(55.0);
            _sensorProviderMock.Setup(s => s.GetFanSpeed()).Returns(2500.0);

            var module = new HardwareDiagnosticModule(_resourceMonitorMock.Object, _sensorProviderMock.Object);

            // Act
            var result = await module.ExecuteAsync();

            // Assert
            Assert.Equal("Hardware", result.ModuleName);
            Assert.Equal(DiagnosticHealthStatus.Critical, result.Status); // Low RAM forces Critical
            Assert.Contains(result.Errors, e => e.Contains("memory is nearly exhausted"));
            Assert.Contains(result.Warnings, w => w.Contains("storage free space is low"));
            Assert.Contains(result.Warnings, w => w.Contains("hardware temperature detected"));

            // Check findings
            Assert.Contains(result.Findings, f => f.Key == "CpuUsageLimitExceeded");
            Assert.Contains(result.Findings, f => f.Key == "LowAvailableRam");
            Assert.Contains(result.Findings, f => f.Key == "LowFreeSpace");
            Assert.Contains(result.Findings, f => f.Key == "HighHardwareTemp");
        }

        [Fact]
        public void Test_RecommendationEngine_Compiles_ActionableRecommendations()
        {
            // Arrange
            var engine = new DiagnosticsRecommendationEngine();
            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding { Key = "CpuUsageLimitExceeded", Value = "95.5%", Subsystem = "Hardware", IsAnomaly = true },
                new DiagnosticFinding { Key = "LowAvailableRam", Value = "256 MB", Subsystem = "Hardware", IsAnomaly = true },
                new DiagnosticFinding { Key = "ServerConnectionLost", Value = "Disconnected", Subsystem = "Network", IsAnomaly = true }
            };

            // Act
            var recommendations = engine.Evaluate(findings).ToList();

            // Assert
            Assert.Equal(3, recommendations.Count);

            var cpuRec = recommendations.First(r => r.Category == "Hardware" && r.Description.Contains("CPU"));
            Assert.Equal("Critical", cpuRec.Severity);
            Assert.Equal("High", cpuRec.Priority);
            Assert.Equal("Hardware", cpuRec.AffectedSubsystem);
            Assert.Contains("Identify and terminate background", cpuRec.RecommendedAction);

            var netRec = recommendations.First(r => r.Category == "Network");
            Assert.Equal("Critical", netRec.Severity);
            Assert.Equal("High", netRec.Priority);
            Assert.Equal("Network", netRec.AffectedSubsystem);
            Assert.Contains("regional router settings", netRec.RecommendedAction);
        }

        [Fact]
        public async Task Test_DatabaseDiagnosticModule_ReportsAvailabilityAndFailures()
        {
            // Arrange
            var module = new DatabaseDiagnosticModule(_performanceMonitorMock.Object);

            // Act
            var result = await module.ExecuteAsync();

            // Assert
            Assert.Equal("Database", result.ModuleName);
            Assert.Equal(DiagnosticHealthStatus.Healthy, result.Status);
            Assert.Equal("True", result.Data["DatabaseAvailable"]);
        }

        [Fact]
        public async Task Test_NetworkDiagnosticModule_ReportsConnectivityAndLatency()
        {
            // Arrange
            var module = new NetworkDiagnosticModule(_performanceMonitorMock.Object);

            // Act
            var result = await module.ExecuteAsync();

            // Assert
            Assert.Equal("Network", result.ModuleName);
            Assert.Equal(DiagnosticHealthStatus.Healthy, result.Status);
            Assert.Equal("True", result.Data["EndpointConnected"]);
        }

        [Fact]
        public async Task Test_SecurityDiagnosticModule_DetectsValidationAnomalies()
        {
            // Arrange
            _securityServiceMock.Setup(s => s.ValidateConfigurationAsync(It.IsAny<CancellationToken>()))
                                .ReturnsAsync(new SecurityValidationResult { ValidationState = SecurityValidationState.Tampered });
            _securityServiceMock.Setup(s => s.ValidateDatabaseAsync(It.IsAny<CancellationToken>()))
                                .ReturnsAsync(new SecurityValidationResult { ValidationState = SecurityValidationState.Passed });
            _securityServiceMock.Setup(s => s.ValidateExecutableAsync(It.IsAny<CancellationToken>()))
                                .ReturnsAsync(new SecurityValidationResult { ValidationState = SecurityValidationState.Passed });

            var module = new SecurityDiagnosticModule(_securityServiceMock.Object);

            // Act
            var result = await module.ExecuteAsync();

            // Assert
            Assert.Equal("Security", result.ModuleName);
            Assert.Equal(DiagnosticHealthStatus.Critical, result.Status);
            Assert.Contains(result.Findings, f => f.Key == "ConfigSignatureTampered");
        }

        [Fact]
        public async Task Test_DiagnosticsEngine_Orchestrates_AllModules_Concurrently()
        {
            // Arrange
            var modules = new List<IDiagnosticModule>
            {
                new HardwareDiagnosticModule(_resourceMonitorMock.Object, _sensorProviderMock.Object),
                new OsDiagnosticModule(),
                new RuntimeDiagnosticModule(_serviceProviderMock.Object),
                new NetworkDiagnosticModule(_performanceMonitorMock.Object),
                new DatabaseDiagnosticModule(_performanceMonitorMock.Object),
                new StorageDiagnosticModule(),
                new SecurityDiagnosticModule(),
                new PluginsDiagnosticModule(),
                new ConfigurationDiagnosticModule(),
                new IpcDiagnosticModule(_performanceMonitorMock.Object),
                new SynchronizationDiagnosticModule(_serviceProviderMock.Object),
                new NotificationsDiagnosticModule(),
                new DownloadsDiagnosticModule(_performanceMonitorMock.Object),
                new UpdatesDiagnosticModule(),
                new OverlayDiagnosticModule(),
                new WatchdogDiagnosticModule()
            };

            var recommendationEngine = new DiagnosticsRecommendationEngine();

            var engine = new DiagnosticsEngine(
                modules,
                recommendationEngine,
                _loggerMock.Object);

            // Act
            var report = await engine.GenerateDiagnosticsReportAsync();

            // Assert
            Assert.NotNull(report);
            Assert.True(report.Timestamp <= DateTime.UtcNow);
            Assert.Equal(Environment.MachineName, report.MachineId);
            Assert.Contains("compiled successfully with 16 modules", report.MachineSummary);
            Assert.Equal(16, report.SubsystemStatus.Count);

            // Verify alphabetical sorted deterministic order of subsystem mappings
            var keys = report.SubsystemStatus.Keys.ToList();
            var sortedKeys = keys.OrderBy(k => k).ToList();
            Assert.Equal(sortedKeys, keys);
        }

        [Fact]
        public async Task Test_DiagnosticsEngine_Resilient_To_SingleModuleFailure()
        {
            // Arrange
            var mockModuleGood = new Mock<IDiagnosticModule>();
            mockModuleGood.Setup(m => m.Name).Returns("GoodModule");
            mockModuleGood.Setup(m => m.AffectedSubsystem).Returns("GoodSubsystem");
            mockModuleGood.Setup(m => m.ExecuteAsync(It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new DiagnosticModuleResult { ModuleName = "GoodModule" });

            var mockModuleBad = new Mock<IDiagnosticModule>();
            mockModuleBad.Setup(m => m.Name).Returns("BadModule");
            mockModuleBad.Setup(m => m.AffectedSubsystem).Returns("BadSubsystem");
            mockModuleBad.Setup(m => m.ExecuteAsync(It.IsAny<CancellationToken>()))
                         .ThrowsAsync(new InvalidOperationException("Fatal database corruption simulated"));

            var modules = new List<IDiagnosticModule> { mockModuleGood.Object, mockModuleBad.Object };
            var recommendationEngine = new DiagnosticsRecommendationEngine();

            var engine = new DiagnosticsEngine(
                modules,
                recommendationEngine,
                _loggerMock.Object);

            // Act
            var report = await engine.GenerateDiagnosticsReportAsync();

            // Assert
            Assert.NotNull(report);
            Assert.Equal(2, report.SubsystemStatus.Count);
            Assert.Equal("Healthy", report.SubsystemStatus["GoodModule"]);
            Assert.Equal("Unknown", report.SubsystemStatus["BadModule"]); // Fallback health on crash
            Assert.Contains(report.Errors, e => e.Contains("Fatal database corruption simulated"));
        }

        [Fact]
        public async Task Test_DiagnosticsEngine_Obeys_Cancellation()
        {
            // Arrange
            var mockModule = new Mock<IDiagnosticModule>();
            mockModule.Setup(m => m.Name).Returns("ModuleA");
            mockModule.Setup(m => m.ExecuteAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new DiagnosticModuleResult { ModuleName = "ModuleA" });

            var modules = new List<IDiagnosticModule> { mockModule.Object };
            var recommendationEngine = new DiagnosticsRecommendationEngine();

            var engine = new DiagnosticsEngine(
                modules,
                recommendationEngine,
                _loggerMock.Object);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.GenerateDiagnosticsReportAsync(cts.Token));
        }
    }
}
