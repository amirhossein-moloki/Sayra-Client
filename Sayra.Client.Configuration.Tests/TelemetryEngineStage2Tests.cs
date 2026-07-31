using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Telemetry;
using Sayra.Client.Shared.Telemetry.Collectors.Hardware;
using Sayra.Client.Shared.Telemetry.Collectors.Runtime;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// Comprehensive high-rigor test suite validating Stage 2 Telemetry Engine features.
    /// Covers pipeline processing, collectors, scheduler, fault isolation, timeout protection, and DI.
    /// </summary>
    public class TelemetryEngineStage2Tests
    {
        private readonly ILogger<TelemetryPipeline> _nullPipelineLogger = NullLogger<TelemetryPipeline>.Instance;
        private readonly ILogger<TelemetryService> _nullServiceLogger = NullLogger<TelemetryService>.Instance;
        private readonly ILogger<MetricsCollector> _nullMetricsLogger = NullLogger<MetricsCollector>.Instance;

        [Fact]
        public async Task BaseTelemetryCollector_MeasuresDurationAndSucceeds()
        {
            var sensorProvider = new HardwareSensorProvider();
            var logger = NullLogger<CpuCollector>.Instance;
            var collector = new CpuCollector(sensorProvider, logger);

            Assert.Equal("CPU Collector", collector.Name);
            Assert.Equal(CollectionInterval.Hardware, collector.Interval);
            Assert.Equal(80, collector.Priority);

            var records = await collector.CollectRecordsAsync();

            Assert.NotNull(records);
            Assert.NotEmpty(records);
            Assert.True(collector.LastExecutionDuration > TimeSpan.Zero);

            var usageRecord = records.FirstOrDefault(r => r.MetricName == "system.cpu.usage");
            Assert.NotNull(usageRecord);
            Assert.Equal(MetricCategory.Cpu, usageRecord.Category);
            Assert.Equal(MetricUnit.Percent, usageRecord.Unit);
            Assert.True(usageRecord.Value >= 0);
        }

        [Fact]
        public async Task BaseTelemetryCollector_IsolatesTimeout_ReturnsEmpty()
        {
            var logger = NullLogger<MockTimeoutCollector>.Instance;
            var collector = new MockTimeoutCollector(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(50), logger);

            var records = await collector.CollectRecordsAsync();

            Assert.NotNull(records);
            Assert.Empty(records);
            Assert.True(collector.LastExecutionDuration >= TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public async Task BaseTelemetryCollector_IsolatesExceptions_ReturnsEmpty()
        {
            var logger = NullLogger<MockFailingCollector>.Instance;
            var collector = new MockFailingCollector(logger);

            var records = await collector.CollectRecordsAsync();

            Assert.NotNull(records);
            Assert.Empty(records);
        }

        [Fact]
        public void TelemetryPipeline_ValidatesAndRejectsInvalidRecords()
        {
            var pipeline = new TelemetryPipeline(_nullPipelineLogger);

            // 1. Missing MetricName
            var record1 = new TelemetryRecord
            {
                Timestamp = DateTime.UtcNow,
                MachineId = "WORKSTATION-01",
                MetricName = "",
                Category = MetricCategory.Cpu,
                Value = 10.0,
                Unit = MetricUnit.Percent
            };
            Assert.False(pipeline.ProcessAndQueue(record1));

            // 2. Default/Missing Timestamp
            var record2 = new TelemetryRecord
            {
                Timestamp = default,
                MachineId = "WORKSTATION-01",
                MetricName = "cpu.usage",
                Category = MetricCategory.Cpu,
                Value = 10.0,
                Unit = MetricUnit.Percent
            };
            Assert.False(pipeline.ProcessAndQueue(record2));

            // 3. Null MachineId
            var record3 = new TelemetryRecord
            {
                Timestamp = DateTime.UtcNow,
                MachineId = null!,
                MetricName = "cpu.usage",
                Category = MetricCategory.Cpu,
                Value = 10.0,
                Unit = MetricUnit.Percent
            };
            Assert.False(pipeline.ProcessAndQueue(record3));

            // 4. NaN value
            var record4 = new TelemetryRecord
            {
                Timestamp = DateTime.UtcNow,
                MachineId = "WORKSTATION-01",
                MetricName = "cpu.usage",
                Category = MetricCategory.Cpu,
                Value = double.NaN,
                Unit = MetricUnit.Percent
            };
            Assert.False(pipeline.ProcessAndQueue(record4));
        }

        [Fact]
        public async Task TelemetryPipeline_NormalizesAndEnrichesValidRecord()
        {
            var pipeline = new TelemetryPipeline(_nullPipelineLogger);
            var record = new TelemetryRecord
            {
                Timestamp = DateTime.UtcNow,
                MachineId = "  workstation-01  ",
                MetricName = "  System.Cpu.Usage  ",
                Category = MetricCategory.Cpu,
                Value = 45.1278,
                Unit = MetricUnit.Percent,
                Severity = MetricSeverity.Warning
            };

            bool success = pipeline.ProcessAndQueue(record);
            Assert.True(success);

            // Read processed record from Channel
            var spinCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var processed = await pipeline.Reader.ReadAsync(spinCts.Token);

            Assert.NotNull(processed);
            Assert.Equal("system.cpu.usage", processed.MetricName);
            Assert.Equal("WORKSTATION-01", processed.MachineId);
            Assert.Equal(45.13, processed.Value); // Rounded to 2 decimal places
            Assert.NotNull(processed.CorrelationId); // Auto-generated if missing

            // Tags Enrichment verification
            Assert.True(processed.Tags.ContainsKey("env"));
            Assert.Equal("Production", processed.Tags["env"]);
            Assert.True(processed.Tags.ContainsKey("os_platform"));
            Assert.True(processed.Tags.ContainsKey("app_version"));
            Assert.Equal("2.0.0", processed.Tags["app_version"]);
        }

        [Fact]
        public async Task MetricsCollector_ManualRecordingAndCycleRetrieval_Succeeds()
        {
            var pipeline = new TelemetryPipeline(_nullPipelineLogger);
            var mockOptions = Options.Create(new CollectionOptions());
            var telemetryService = new TelemetryService(pipeline, Enumerable.Empty<IExtendedTelemetryCollector>(), mockOptions, _nullServiceLogger);
            var metricsCollector = new MetricsCollector(telemetryService, _nullMetricsLogger);

            await metricsCollector.RecordMetricAsync("custom.cpu.usage", 82.5);
            await metricsCollector.RecordMetricAsync("custom.ram.used_mb", 2048.0);

            var firstCycle = await metricsCollector.GetCollectedMetricsAsync();
            Assert.Equal(2, firstCycle.Count);

            var cpuPoint = firstCycle.FirstOrDefault(p => p.Value == 82.5);
            Assert.NotNull(cpuPoint);

            // Second retrieval cycle must be empty as queue was cleared
            var secondCycle = await metricsCollector.GetCollectedMetricsAsync();
            Assert.Empty(secondCycle);
        }

        [Fact]
        public async Task TelemetryService_CollectionOrchestration_StartsAndStopsGracefully()
        {
            var pipeline = new TelemetryPipeline(_nullPipelineLogger);
            var mockOptions = Options.Create(new CollectionOptions
            {
                CriticalIntervalSeconds = 1,
                PerformanceIntervalSeconds = 1,
                HardwareIntervalSeconds = 1,
                StorageIntervalSeconds = 1,
                HistoricalIntervalSeconds = 1
            });

            var sensorProvider = new HardwareSensorProvider();
            var cpuCollector = new CpuCollector(sensorProvider, NullLogger<CpuCollector>.Instance);
            var watchdogCollector = new WatchdogCollector(NullLogger<WatchdogCollector>.Instance);

            var collectors = new List<IExtendedTelemetryCollector> { cpuCollector, watchdogCollector };
            using var telemetryService = new TelemetryService(pipeline, collectors, mockOptions, _nullServiceLogger);

            // Verify starting loops
            await telemetryService.StartCollectionAsync();

            // Give it 1.5 seconds to run the first collections
            await Task.Delay(1500);

            // Read some produced items from the pipeline channel to ensure scheduling loops execute
            var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var cpuUsageRecord = await pipeline.Reader.ReadAsync(tokenSource.Token);
            Assert.NotNull(cpuUsageRecord);

            // Verify stopping loops
            await telemetryService.StopCollectionAsync();
        }

        [Fact]
        public void DependencyInjection_RegistersAndResolvesAllServices()
        {
            var services = new ServiceCollection();
            var configData = new Dictionary<string, string?>
            {
                { "Observability:Telemetry:EnableTelemetry", "true" },
                { "Observability:Collection:CriticalIntervalSeconds", "5" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            services.AddObservabilityServices(configuration);
            services.AddLogging();

            var provider = services.BuildServiceProvider();

            // Resolve Infrastructure
            var sensorProvider = provider.GetService<IHardwareSensorProvider>();
            Assert.NotNull(sensorProvider);

            var pipeline = provider.GetService<TelemetryPipeline>();
            Assert.NotNull(pipeline);

            var telemetryService = provider.GetService<ITelemetryService>();
            Assert.NotNull(telemetryService);

            var metricsCollector = provider.GetService<IMetricsCollector>();
            Assert.NotNull(metricsCollector);

            // Resolve Collectors
            var collectors = provider.GetServices<IExtendedTelemetryCollector>().ToList();
            Assert.Equal(16, collectors.Count);

            // Verify individual type coverage
            Assert.Contains(collectors, c => c is CpuCollector);
            Assert.Contains(collectors, c => c is MemoryCollector);
            Assert.Contains(collectors, c => c is GpuCollector);
            Assert.Contains(collectors, c => c is DiskCollector);
            Assert.Contains(collectors, c => c is NetworkCollector);
            Assert.Contains(collectors, c => c is ProcessesCollector);
            Assert.Contains(collectors, c => c is WindowsSessionsCollector);
            Assert.Contains(collectors, c => c is PluginsCollector);
            Assert.Contains(collectors, c => c is WatchdogCollector);
            Assert.Contains(collectors, c => c is PolicyCollector);
            Assert.Contains(collectors, c => c is DownloadsCollector);
            Assert.Contains(collectors, c => c is UpdatesCollector);
            Assert.Contains(collectors, c => c is IpcCollector);
            Assert.Contains(collectors, c => c is SyncCollector);
            Assert.Contains(collectors, c => c is NotificationCollector);
            Assert.Contains(collectors, c => c is OverlayCollector);
        }

        // --- Mocks to Validate Isolation and Timeouts ---

        private class MockTimeoutCollector : BaseTelemetryCollector
        {
            private readonly TimeSpan _workDelay;

            public MockTimeoutCollector(TimeSpan workDelay, TimeSpan timeout, ILogger<MockTimeoutCollector> logger)
                : base("Mock Timeout Collector", CollectionInterval.Performance, 10, timeout, logger)
            {
                _workDelay = workDelay;
            }

            protected override async Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
            {
                await Task.Delay(_workDelay, cancellationToken);
                return new[]
                {
                    new TelemetryRecord { MetricName = "mock.timeout.success", Value = 1.0 }
                };
            }
        }

        private class MockFailingCollector : BaseTelemetryCollector
        {
            public MockFailingCollector(ILogger<MockFailingCollector> logger)
                : base("Mock Failing Collector", CollectionInterval.Performance, 10, TimeSpan.FromSeconds(5), logger)
            {
            }

            protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Simulated hardware sensor hardware exception.");
            }
        }
    }
}
