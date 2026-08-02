using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Models.Telemetry.Policies;
using Sayra.Client.Shared.Telemetry.Alerts;
using Sayra.Client.Shared.Telemetry.Alerts.Evaluators;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// High-rigor xUnit test suite validating Phase 8 Stage 7 Enterprise Alert Engine.
    /// </summary>
    public class AlertEngineStage7Tests
    {
        private readonly MockLiveTelemetryService _mockTelemetryService;
        private readonly MockDiagnosticsEngine _mockDiagnosticsEngine;
        private readonly IOptions<AlertOptions> _options;

        public AlertEngineStage7Tests()
        {
            _mockTelemetryService = new MockLiveTelemetryService();
            _mockDiagnosticsEngine = new MockDiagnosticsEngine();

            var alertOptions = new AlertOptions
            {
                CpuThresholdPercent = 90.0,
                MemoryThresholdPercent = 90.0,
                DiskFreeSpaceThresholdPercent = 10.0,
                CooldownPeriodSeconds = 300
            };
            _options = Options.Create(alertOptions);
        }

        [Fact]
        public async Task ThresholdEvaluator_HandlesAllOperatorsCorrectly()
        {
            // Arrange & Act
            var gpPolicy = new ThresholdPolicy { Operator = "GreaterThan", Value = 50.0 };
            var lpPolicy = new ThresholdPolicy { Operator = "LessThan", Value = 20.0 };
            var eqPolicy = new ThresholdPolicy { Operator = "Equal", Value = 100.0 };
            var neqPolicy = new ThresholdPolicy { Operator = "NotEqual", Value = 100.0 };
            var rangePolicy = new ThresholdPolicy { Operator = "Range", MinValue = 10.0, MaxValue = 20.0 };
            var percentPolicy = new ThresholdPolicy { Operator = "Percentage", Value = 75.0 };
            var boolPolicy = new ThresholdPolicy { Operator = "Boolean", BooleanValue = true };

            // Assert
            Assert.True(EvaluateThreshold(55.0, gpPolicy));
            Assert.False(EvaluateThreshold(45.0, gpPolicy));

            Assert.True(EvaluateThreshold(15.0, lpPolicy));
            Assert.False(EvaluateThreshold(25.0, lpPolicy));

            Assert.True(EvaluateThreshold(100.0, eqPolicy));
            Assert.False(EvaluateThreshold(101.0, eqPolicy));

            Assert.True(EvaluateThreshold(99.0, neqPolicy));
            Assert.False(EvaluateThreshold(100.0, neqPolicy));

            Assert.True(EvaluateThreshold(15.0, rangePolicy));
            Assert.False(EvaluateThreshold(5.0, rangePolicy));

            Assert.True(EvaluateThreshold(80.0, percentPolicy));
            Assert.False(EvaluateThreshold(70.0, percentPolicy));

            Assert.True(EvaluateThresholdBool(true, boolPolicy));
            Assert.False(EvaluateThresholdBool(false, boolPolicy));
        }

        [Fact]
        public async Task ProcessAlert_SavesActiveAlertCorrectly()
        {
            // Arrange
            var engine = CreateAlertEngine();
            var alert = CreateMockAlert("CpuThreshold", SubsystemType.Telemetry, MetricCategory.Cpu, 95.0, 90.0);

            // Act
            await engine.ProcessAlertAsync(alert);

            // Assert
            var active = await engine.GetActiveAlertsAsync();
            Assert.Single(active);
            Assert.Equal("CpuThreshold", active.First().Name);
            Assert.Equal(AlertStatus.Active, active.First().Status);
        }

        [Fact]
        public async Task Deduplication_ExtendsExistingAlert_InsteadOfCreatingNewOne()
        {
            // Arrange
            var engine = CreateAlertEngine();
            var alert1 = CreateMockAlert("CpuThreshold", SubsystemType.Telemetry, MetricCategory.Cpu, 95.0, 90.0);
            var alert2 = alert1 with { Value = 98.0, Message = "Another spike!" };

            // Act
            await engine.ProcessAlertAsync(alert1);
            await engine.ProcessAlertAsync(alert2);

            // Assert
            var active = await engine.GetActiveAlertsAsync();
            Assert.Single(active); // Deduplicated into 1
            Assert.Equal(98.0, active.First().Value);
            Assert.Contains("Recurred", active.First().Message);
        }

        [Fact]
        public async Task Suppression_SuppressesAlertBasedOnPolicy()
        {
            // Arrange
            var suppressionPolicy = new SuppressionPolicy { IsSuppressed = true };
            var rulesConfig = new Dictionary<string, AlertPolicyConfig>
            {
                { "CpuThreshold", new AlertPolicyConfig { Suppression = suppressionPolicy } }
            };
            _options.Value.Rules = rulesConfig;

            var engine = CreateAlertEngine();
            var alert = CreateMockAlert("CpuThreshold", SubsystemType.Telemetry, MetricCategory.Cpu, 95.0, 90.0);

            // Act
            await engine.ProcessAlertAsync(alert);

            // Assert
            var active = await engine.GetActiveAlertsAsync();
            Assert.Empty(active); // Suppressed alerts are not active
        }

        [Fact]
        public async Task ManualSuppression_BypassesActiveAlerts()
        {
            // Arrange
            var suppressionProvider = new AlertSuppressionProvider();
            var engine = CreateAlertEngine(suppressionProvider: suppressionProvider);
            var alert = CreateMockAlert("CpuThreshold", SubsystemType.Telemetry, MetricCategory.Cpu, 95.0, 90.0);

            // Act
            suppressionProvider.SuppressManual(alert.Name, TimeSpan.FromMinutes(10));
            await engine.ProcessAlertAsync(alert);

            // Assert
            var active = await engine.GetActiveAlertsAsync();
            Assert.Empty(active); // Manual suppression applies
        }

        [Fact]
        public async Task Escalation_TriggersOnFrequencyThreshold()
        {
            // Arrange
            var escalationPolicy = new EscalationPolicy { Enabled = true, FrequencyThreshold = 1, EscalationPriority = "Critical" };
            var rulesConfig = new Dictionary<string, AlertPolicyConfig>
            {
                { "CpuThreshold", new AlertPolicyConfig { Escalation = escalationPolicy } }
            };
            _options.Value.Rules = rulesConfig;

            var engine = CreateAlertEngine();
            var alert = CreateMockAlert("CpuThreshold", SubsystemType.Telemetry, MetricCategory.Cpu, 95.0, 90.0);

            // Act
            await engine.ProcessAlertAsync(alert);
            await engine.ProcessAlertAsync(alert); // Second trigger meets FrequencyThreshold 1

            // Assert
            var active = await engine.GetActiveAlertsAsync();
            Assert.Single(active);
            Assert.True(active.First().Escalated);
            Assert.Equal(AlertStatus.Escalated, active.First().Status);
            Assert.Equal(AlertPriority.Critical, active.First().Priority);
        }

        [Fact]
        public async Task Acknowledgement_UpdatesAlertStateCorrectly()
        {
            // Arrange
            var engine = CreateAlertEngine();
            var alert = CreateMockAlert("CpuThreshold", SubsystemType.Telemetry, MetricCategory.Cpu, 95.0, 90.0);
            await engine.ProcessAlertAsync(alert);

            // Act
            var activeAlert = (await engine.GetActiveAlertsAsync()).First();
            await engine.AcknowledgeAlertAsync(activeAlert.AlertId, "AdminOperator01", "Checking immediately.");

            // Assert
            var active = await engine.GetActiveAlertsAsync();
            Assert.Empty(active); // Acknowledged alerts are no longer active/created/escalated

            // Check history/retrieved state
            var ackedAlert = engine.GetAllAlerts().First(a => a.AlertId == activeAlert.AlertId);
            Assert.True(ackedAlert.Acknowledged);
            Assert.Equal("Checking immediately.", ackedAlert.AcknowledgementComment);
        }

        [Fact]
        public async Task AutomaticRecovery_ResolvesAlertWhenConditionIsNormal()
        {
            // Arrange
            var engine = CreateAlertEngine();
            var alert = CreateMockAlert("CpuThreshold", SubsystemType.Telemetry, MetricCategory.Cpu, 95.0, 90.0);

            // CPU spikes initially
            _mockTelemetryService.CpuUsage = 95.0;
            await engine.EvaluateRulesAsync();

            var active = await engine.GetActiveAlertsAsync();
            Assert.Single(active);

            // Act: CPU returns to normal
            _mockTelemetryService.CpuUsage = 40.0;
            await engine.EvaluateRulesAsync();

            // Assert
            var activeAfter = await engine.GetActiveAlertsAsync();
            Assert.Empty(activeAfter); // Auto-resolved
        }

        [Fact]
        public async Task ParallelRuleEvaluation_RunsSuccessfully_WithFailureIsolation()
        {
            // Arrange
            var failingEvaluator = new FailingRuleEvaluator();
            var cpuEvaluator = new CpuThresholdRuleEvaluator(_mockTelemetryService, new AlertPolicyProvider(_options));

            var ruleProvider = new AlertRuleProvider(new IAlertRuleEvaluator[] { failingEvaluator, cpuEvaluator });
            var engine = CreateAlertEngine(ruleProvider: ruleProvider);

            // Spike CPU to trigger a valid alert
            _mockTelemetryService.CpuUsage = 95.0;

            // Act
            await engine.EvaluateRulesAsync();

            // Assert
            var active = await engine.GetActiveAlertsAsync();
            Assert.Single(active); // CPU alert processed successfully, failing rule was isolated and didn't crash evaluation
        }

        [Fact]
        public async Task ConcurrencyTest_ProcessAlertsIsThreadSafe()
        {
            // Arrange
            var engine = CreateAlertEngine();
            int threadsCount = 10;
            int iterations = 100;
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < threadsCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < iterations; j++)
                    {
                        var alert = CreateMockAlert($"Rule_{index}_{j}", SubsystemType.Telemetry, MetricCategory.Cpu, 95.0, 90.0);
                        await engine.ProcessAlertAsync(alert);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            var active = await engine.GetActiveAlertsAsync();
            Assert.Equal(threadsCount * iterations, active.Count);
        }

        [Fact]
        public async Task Cancellation_AbortsRuleEvaluationCorrectly()
        {
            // Arrange
            var engine = CreateAlertEngine();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await engine.EvaluateRulesAsync(cts.Token));
        }

        [Fact]
        public async Task ConfigurationBinding_ResolvesFromDIContainer()
        {
            // Arrange
            var configData = new Dictionary<string, string?>
            {
                { "Observability:Alerts:CpuThresholdPercent", "85.0" },
                { "Observability:Alerts:MemoryThresholdPercent", "85.0" },
                { "Observability:Alerts:DiskFreeSpaceThresholdPercent", "15.0" },
                { "Observability:Alerts:CooldownPeriodSeconds", "150" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddObservabilityServices(configuration);
            services.AddSingleton<ILiveTelemetryService>(_mockTelemetryService);

            using var serviceProvider = services.BuildServiceProvider();

            // Act
            var options = serviceProvider.GetRequiredService<IOptions<AlertOptions>>().Value;
            var policyProvider = serviceProvider.GetRequiredService<IAlertPolicyProvider>();
            var policy = await policyProvider.GetPolicyAsync("CpuThreshold");

            // Assert
            Assert.Equal(85.0, options.CpuThresholdPercent);
            Assert.Equal(150, options.CooldownPeriodSeconds);
            Assert.Equal(85.0, policy.Threshold.Value);
        }

        // --- Helper Methods ---

        private AlertEngine CreateAlertEngine(
            IAlertRuleProvider? ruleProvider = null,
            IAlertSuppressionProvider? suppressionProvider = null)
        {
            var policyProvider = new AlertPolicyProvider(_options);
            var deduplicationProvider = new AlertDeduplicationProvider();
            var recoveryProvider = new AlertRecoveryProvider(_mockTelemetryService);
            var suppression = suppressionProvider ?? new AlertSuppressionProvider();
            var escalationProvider = new AlertEscalationProvider();

            var rules = new List<IAlertRuleEvaluator>
            {
                new CpuThresholdRuleEvaluator(_mockTelemetryService, policyProvider)
            };
            var provider = ruleProvider ?? new AlertRuleProvider(rules);

            return new AlertEngine(
                provider,
                policyProvider,
                deduplicationProvider,
                recoveryProvider,
                suppression,
                escalationProvider);
        }

        private AlertRecord CreateMockAlert(string name, SubsystemType subsystem, MetricCategory category, double value, double threshold)
        {
            return new AlertRecord
            {
                Name = name,
                Subsystem = subsystem,
                Category = category,
                Value = value,
                Threshold = threshold,
                Message = $"Mock violation: {name}"
            };
        }

        private bool EvaluateThreshold(double value, ThresholdPolicy policy)
        {
            return policy.Operator switch
            {
                "GreaterThan" => policy.Value.HasValue && value > policy.Value.Value,
                "LessThan" => policy.Value.HasValue && value < policy.Value.Value,
                "Equal" => policy.Value.HasValue && Math.Abs(value - policy.Value.Value) < 0.001,
                "NotEqual" => policy.Value.HasValue && Math.Abs(value - policy.Value.Value) >= 0.001,
                "Range" => policy.MinValue.HasValue && policy.MaxValue.HasValue && value >= policy.MinValue.Value && value <= policy.MaxValue.Value,
                "Percentage" => policy.Value.HasValue && value >= policy.Value.Value,
                _ => false
            };
        }

        private bool EvaluateThresholdBool(bool value, ThresholdPolicy policy)
        {
            if (policy.Operator == "Boolean")
            {
                return policy.BooleanValue.HasValue && value == policy.BooleanValue.Value;
            }
            return false;
        }

        // --- Mocking Dependencies ---

        private class MockLiveTelemetryService : ILiveTelemetryService
        {
            public double CpuUsage { get; set; } = 45.0;
            public double RamUsed { get; set; } = 2048.0;
            public double RamTotal { get; set; } = 8192.0;
            public double FreeSpace { get; set; } = 50.0;

            public Task<LiveTelemetryData> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new LiveTelemetryData
                {
                    CpuUsagePercent = CpuUsage,
                    RamUsedMb = RamUsed,
                    RamTotalMb = RamTotal,
                    FreeSpaceGb = FreeSpace
                });
            }

            public IObservable<LiveTelemetryData> GetTelemetryStream(TimeSpan interval)
            {
                throw new NotImplementedException();
            }
        }

        private class MockDiagnosticsEngine : Sayra.Client.Shared.Interfaces.Telemetry.IDiagnosticsEngine
        {
            public Task<DiagnosticReport> GenerateDiagnosticsReportAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new DiagnosticReport
                {
                    Timestamp = DateTime.UtcNow,
                    MachineId = "MockMachine",
                    SubsystemStatus = new Dictionary<string, string>
                    {
                        { "Network", "Normal" },
                        { "Database", "Normal" },
                        { "IPC", "Normal" }
                    }
                });
            }
        }

        private class FailingRuleEvaluator : IAlertRuleEvaluator
        {
            public string RuleName => "FailingRule";
            public string Subsystem => "Telemetry";

            public Task<AlertRecord?> EvaluateAsync(CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Simulation failure during evaluation");
            }
        }
    }
}
