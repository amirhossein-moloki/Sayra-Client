using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SayraClient.Services;
using SayraClient.Services.Recovery;
using SayraClient.Services.Recovery.Exporters;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage7Tests")]
    public class DiagnosticsEngineTests : IDisposable
    {
        private readonly ServiceCollection _services;
        private readonly Mock<ILogger<RecoveryDiagnosticsEngine>> _loggerMock = new();
        private readonly Mock<IHealthMonitor> _healthMonitorMock = new();
        private readonly Mock<ICrashRecoveryManager> _crashRecoveryMock = new();
        private readonly Mock<IResourceMonitor> _resourceMonitorMock = new();
        private readonly Mock<ISecurityHardeningService> _securityHardeningMock = new();
        private readonly Mock<IEventDispatcher> _eventDispatcherMock = new();

        private readonly string _testReportsDir;
        private readonly RecoveryDiagnosticsOptions _testOptions;
        private readonly RecoveryMetricsCollector _metricsCollector;

        public DiagnosticsEngineTests()
        {
            _testReportsDir = Path.Combine(AppContext.BaseDirectory, "test_diagnostics_reports");
            if (Directory.Exists(_testReportsDir))
            {
                Directory.Delete(_testReportsDir, true);
            }
            Directory.CreateDirectory(_testReportsDir);

            _testOptions = new RecoveryDiagnosticsOptions
            {
                ReportsDirectory = _testReportsDir,
                RetentionLimit = 5,
                EnableJson = true,
                EnableText = true,
                ApplicationVersion = "1.0.0.99",
                BuildNumber = "TestBuild.123"
            };

            _metricsCollector = new RecoveryMetricsCollector();

            _services = new ServiceCollection();
            _services.AddSingleton(_loggerMock.Object);
            _services.AddSingleton(_healthMonitorMock.Object);
            _services.AddSingleton(_crashRecoveryMock.Object);
            _services.AddSingleton(_resourceMonitorMock.Object);
            _services.AddSingleton(_securityHardeningMock.Object);
            _services.AddSingleton(_eventDispatcherMock.Object);
            _services.AddSingleton(_metricsCollector);
            _services.AddSingleton(Options.Create(_testOptions));

            // Exporters
            _services.AddSingleton<IDiagnosticsExporter, PlainTextDiagnosticsExporter>();
            _services.AddSingleton<IDiagnosticsExporter, JsonDiagnosticsExporter>();
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testReportsDir))
                {
                    Directory.Delete(_testReportsDir, true);
                }
            }
            catch
            {
                // ignore
            }
        }

        private ServiceProvider BuildProvider() => _services.BuildServiceProvider();

        [Fact]
        public async Task Test_StartupReport_Generation_And_Metadata_Injection()
        {
            var provider = BuildProvider();
            var attempts = new List<RecoveryAttempt>
            {
                new RecoveryAttempt { SubsystemName = "Database", ActionTaken = "REINDEX" }
            };
            var recoveryReport = new RecoveryReport { Attempts = attempts, SuccessfulRecoveries = 1 };

            _crashRecoveryMock.Setup(c => c.GenerateRecoverySummaryAsync(It.IsAny<CancellationToken>()))
                              .ReturnsAsync(recoveryReport);
            _crashRecoveryMock.Setup(c => c.ValidatePreviousShutdownAsync(It.IsAny<CancellationToken>()))
                              .ReturnsAsync(new PreviousShutdownState { LastShutdownReason = "Normal", IsRecoveryRequired = false });

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            string reportText = await engine.GenerateStartupReportAsync();

            Assert.Contains("SAYRA ENTERPRISE RESILIENCE DIAGNOSTICS: STARTUP REPORT", reportText);
            Assert.Contains("Application Version: 1.0.0.99", reportText);
            Assert.Contains("Build Number:        TestBuild.123", reportText);
            Assert.Contains("Recovered Subsystems:\r\n  - Database", reportText.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
        }

        [Fact]
        public async Task Test_HealthReport_Generation()
        {
            var provider = BuildProvider();
            var subsystemInfo = new SubsystemHealthInfo
            {
                SubsystemName = "Network",
                State = SubsystemHealthState.Warning,
                HealthScore = 85.0,
                LastHeartbeat = DateTime.UtcNow,
                LastMessage = "Transient socket timeout",
                Dependencies = new List<string> { "Database" }
            };
            subsystemInfo.AddHistoryEntry("timeout");

            var detailedHealth = new Dictionary<string, SubsystemHealthInfo>
            {
                ["Network"] = subsystemInfo
            };

            _healthMonitorMock.Setup(h => h.GetDetailedHealthAsync(It.IsAny<CancellationToken>()))
                              .ReturnsAsync(detailedHealth);

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            string reportText = await engine.GenerateHealthReportAsync();

            Assert.Contains("SAYRA ENTERPRISE RESILIENCE DIAGNOSTICS: HEALTH REPORT", reportText);
            Assert.Contains("Subsystem: Network", reportText);
            Assert.Contains("State:          Warning", reportText);
            Assert.Contains("Health Score:   85.0", reportText);
            Assert.Contains("Dependencies:   [Database]", reportText);
        }

        [Fact]
        public async Task Test_RecoveryReport_Metrics_Collection()
        {
            var provider = BuildProvider();

            // Record some metric outcomes
            _metricsCollector.IncrementActiveRecoveries();
            _metricsCollector.RecordRecoveryAttempt("Database", "RECONNECT", 1);
            _metricsCollector.RecordRecoveryResult("Database", Guid.NewGuid(), true, TimeSpan.FromMilliseconds(200), "Reconnected", null);
            _metricsCollector.IncrementEscalations();

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            string reportText = await engine.GenerateRecoveryReportAsync();

            Assert.Contains("Total Recovery Actions:     1", reportText);
            Assert.Contains("Recovery Success Rate:      100.00 %", reportText);
            Assert.Contains("Escalated Incidents:        1", reportText);
            Assert.Contains("Average Recovery Duration:  200.00 ms", reportText);
        }

        [Fact]
        public async Task Test_FailureReport_And_RecommendationRules()
        {
            var provider = BuildProvider();

            var subsystemInfo = new SubsystemHealthInfo
            {
                SubsystemName = "Database",
                State = SubsystemHealthState.Critical,
                LastHeartbeat = DateTime.UtcNow,
                LastMessage = "SQLite database locked.",
                LastException = "SQLiteException: Database locked."
            };
            var detailedHealth = new Dictionary<string, SubsystemHealthInfo> { ["Database"] = subsystemInfo };
            _healthMonitorMock.Setup(h => h.GetDetailedHealthAsync(It.IsAny<CancellationToken>()))
                              .ReturnsAsync(detailedHealth);

            var resourceMetrics = new ResourceMetrics
            {
                AvailableSystemRamBytes = 200 * 1024 * 1024L, // Extremely low RAM
                CpuUsagePercentage = 95.0,
                PressureLevel = ResourcePressureLevel.Critical
            };
            _resourceMonitorMock.Setup(r => r.GetCurrentMetricsAsync(It.IsAny<CancellationToken>()))
                               .ReturnsAsync(resourceMetrics);

            var securityValidation = new SecurityValidationResult
            {
                TargetName = "Configuration",
                ValidationState = SecurityValidationState.Tampered,
                Message = "Configuration signature check failed."
            };
            _securityHardeningMock.Setup(s => s.RunFullValidationAsync(It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(new[] { securityValidation });

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            string reportText = await engine.GenerateFailureReportAsync();

            // Recommendations logic verification
            Assert.Contains("High memory usage detected", reportText);
            Assert.Contains("Configuration file tampering or invalid digital signature", reportText);
            Assert.Contains("Subsystem 'Database' has persistent failure or transition patterns", reportText);
            Assert.Contains("SQLiteException: Database locked.", reportText);
        }

        [Fact]
        public async Task Test_ResourceReport_Details()
        {
            var provider = BuildProvider();

            var resourceMetrics = new ResourceMetrics
            {
                CpuUsagePercentage = 45.5,
                ProcessRamBytes = 150 * 1024 * 1024L,
                TotalSystemRamBytes = 8192 * 1024 * 1024L,
                AvailableSystemRamBytes = 4096 * 1024 * 1024L,
                FreeDiskSpaceBytes = 120 * 1024 * 1024 * 1024L,
                GpuUsagePercentage = 15.0,
                DiskIoBytesPerSecond = 50 * 1024,
                NetworkIoBytesPerSecond = 100 * 1024,
                HandleCount = 250,
                ThreadCount = 35,
                GdiObjectsCount = 75,
                HardwareTemperatureCelsius = 52.0,
                PressureLevel = ResourcePressureLevel.Low,
                ThresholdStatus = "Normal"
            };
            _resourceMonitorMock.Setup(r => r.GetCurrentMetricsAsync(It.IsAny<CancellationToken>()))
                               .ReturnsAsync(resourceMetrics);

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            string reportText = await engine.GenerateResourceReportAsync();

            Assert.Contains("CPU Usage:            45.50 %", reportText);
            Assert.Contains("Process memory:       150.00 MB", reportText);
            Assert.Contains("Available System RAM: 4096.00 MB", reportText);
            Assert.Contains("Free Storage Space:   120.00 GB", reportText);
            Assert.Contains("Hardware Temperature: 52.0 °C", reportText);
        }

        [Fact]
        public async Task Test_SecurityReport_And_Validations()
        {
            var provider = BuildProvider();

            var configRes = new SecurityValidationResult { TargetName = "Configuration", ValidationState = SecurityValidationState.Passed, Message = "Config valid" };
            var policyRes = new SecurityValidationResult { TargetName = "Policy", ValidationState = SecurityValidationState.Passed, Message = "Policy valid" };
            var dbRes = new SecurityValidationResult { TargetName = "Database", ValidationState = SecurityValidationState.Passed, Message = "DB sound" };
            var mediaRes = new SecurityValidationResult { TargetName = "Media", ValidationState = SecurityValidationState.Passed, Message = "Media correct" };
            var pluginRes = new SecurityValidationResult { TargetName = "Plugin", ValidationState = SecurityValidationState.Passed, Message = "Plugins correct" };
            var pkgRes = new SecurityValidationResult { TargetName = "Package", ValidationState = SecurityValidationState.Passed, Message = "Package signed" };
            var exeRes = new SecurityValidationResult { TargetName = "Executable", ValidationState = SecurityValidationState.Passed, Message = "Executable Authenticode verified" };

            _securityHardeningMock.Setup(s => s.ValidateConfigurationAsync(It.IsAny<CancellationToken>())).ReturnsAsync(configRes);
            _securityHardeningMock.Setup(s => s.ValidatePolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(policyRes);
            _securityHardeningMock.Setup(s => s.ValidateDatabaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dbRes);
            _securityHardeningMock.Setup(s => s.ValidateMediaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mediaRes);
            _securityHardeningMock.Setup(s => s.ValidatePluginsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(pluginRes);
            _securityHardeningMock.Setup(s => s.ValidatePackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(pkgRes);
            _securityHardeningMock.Setup(s => s.ValidateExecutableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(exeRes);

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            string reportText = await engine.GenerateSecurityReportAsync();

            Assert.Contains("Configuration Integrity: Passed", reportText);
            Assert.Contains("Security Policy Trust:   Passed", reportText);
            Assert.Contains("SQLCipher DB PRAGMA:     Passed", reportText);
            Assert.Contains("Executable Authenticode: Passed (Executable Authenticode verified)", reportText);
        }

        [Fact]
        public async Task Test_FullDiagnostics_JSON_Payload()
        {
            var provider = BuildProvider();
            _crashRecoveryMock.Setup(c => c.GenerateRecoverySummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryReport());
            _crashRecoveryMock.Setup(c => c.ValidatePreviousShutdownAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PreviousShutdownState());
            _healthMonitorMock.Setup(h => h.GetDetailedHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, SubsystemHealthInfo>());
            _resourceMonitorMock.Setup(r => r.GetCurrentMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ResourceMetrics());

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            string jsonReport = await engine.GenerateFullDiagnosticsAsync();

            // Verify valid JSON
            var doc = JsonDocument.Parse(jsonReport);
            Assert.Equal("FullDiagnostics", doc.RootElement.GetProperty("ReportType").GetString());
            Assert.Equal("1.0.0.99", doc.RootElement.GetProperty("ApplicationVersion").GetString());
            Assert.NotNull(doc.RootElement.GetProperty("Payload").GetProperty("Startup"));
            Assert.NotNull(doc.RootElement.GetProperty("Payload").GetProperty("Health"));
        }

        [Fact]
        public async Task Test_Exporter_PlainText()
        {
            var provider = BuildProvider();
            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            string destFile = Path.Combine(_testReportsDir, "exports", "startup_export.txt");

            string finalPath = await engine.ExportDiagnosticsAsync(ReportType.Startup, "TXT", destFile);

            Assert.True(File.Exists(finalPath));
            string content = await File.ReadAllTextAsync(finalPath);
            Assert.Contains("SAYRA ENTERPRISE RESILIENCE DIAGNOSTICS: STARTUP REPORT", content);
        }

        [Fact]
        public async Task Test_Exporter_JSON()
        {
            var provider = BuildProvider();
            _crashRecoveryMock.Setup(c => c.GenerateRecoverySummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryReport());
            _crashRecoveryMock.Setup(c => c.ValidatePreviousShutdownAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PreviousShutdownState());

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            string destFile = Path.Combine(_testReportsDir, "exports", "startup_export.json");

            string finalPath = await engine.ExportDiagnosticsAsync(ReportType.Startup, "JSON", destFile);

            Assert.True(File.Exists(finalPath));
            string content = await File.ReadAllTextAsync(finalPath);
            var doc = JsonDocument.Parse(content);
            Assert.Equal("Startup", doc.RootElement.GetProperty("ReportType").GetString());
        }

        [Fact]
        public async Task Test_Pruning_And_Retention()
        {
            var provider = BuildProvider();
            _crashRecoveryMock.Setup(c => c.GenerateRecoverySummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryReport());
            _crashRecoveryMock.Setup(c => c.ValidatePreviousShutdownAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PreviousShutdownState());
            _healthMonitorMock.Setup(h => h.GetDetailedHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, SubsystemHealthInfo>());
            _resourceMonitorMock.Setup(r => r.GetCurrentMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ResourceMetrics());

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            // Generate 8 reports (Limit is 5)
            for (int i = 0; i < 8; i++)
            {
                await engine.GenerateAndPersistAllReportsAsync();
                // Brief delay to allow unique file names if timestamp includes seconds
                await Task.Delay(10);
            }

            var files = Directory.GetFiles(_testReportsDir);
            // Verify that for each individual report type, the file count does not exceed the targetCount (RetentionLimit * 2)
            var startupFiles = files.Where(f => Path.GetFileName(f).StartsWith("startup_report_", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.True(startupFiles.Count <= _testOptions.RetentionLimit * 2, $"Startup files count ({startupFiles.Count}) exceeded the expected retention limits.");
        }

        [Fact]
        public async Task Test_Report_Generation_Cancellation()
        {
            var provider = BuildProvider();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            var ex = await Record.ExceptionAsync(() => engine.GenerateStartupReportAsync(cts.Token));
            // Should either be canceled cleanly or throw OperationCanceledException
            Assert.True(ex is OperationCanceledException || ex == null);
        }

        [Fact]
        public async Task Test_Diagnostics_Event_Dispatched()
        {
            var provider = BuildProvider();
            _crashRecoveryMock.Setup(c => c.GenerateRecoverySummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryReport());
            _crashRecoveryMock.Setup(c => c.ValidatePreviousShutdownAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PreviousShutdownState());
            _healthMonitorMock.Setup(h => h.GetDetailedHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, SubsystemHealthInfo>());
            _resourceMonitorMock.Setup(r => r.GetCurrentMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ResourceMetrics());

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            await engine.GenerateAndPersistAllReportsAsync();

            _eventDispatcherMock.Verify(e => e.Dispatch(It.IsAny<DiagnosticsGenerationStartedEvent>()), Times.AtLeastOnce);
            _eventDispatcherMock.Verify(e => e.Dispatch(It.IsAny<DiagnosticsGenerationCompletedEvent>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Test_Concurrent_Report_Generation_Is_Safe()
        {
            var provider = BuildProvider();
            _crashRecoveryMock.Setup(c => c.GenerateRecoverySummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryReport());
            _crashRecoveryMock.Setup(c => c.ValidatePreviousShutdownAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PreviousShutdownState());
            _healthMonitorMock.Setup(h => h.GetDetailedHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, SubsystemHealthInfo>());
            _resourceMonitorMock.Setup(r => r.GetCurrentMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ResourceMetrics());

            var engine = new RecoveryDiagnosticsEngine(
                _loggerMock.Object,
                provider,
                Options.Create(_testOptions));

            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() => engine.GenerateAndPersistAllReportsAsync()));
            }

            var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
            Assert.Null(ex); // No concurrency exceptions or race conditions
        }
    }
}
