using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Constants;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Exceptions;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Models.Telemetry.Results;
using Sayra.Client.Shared.Models.Telemetry.ValueObjects;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// High-rigor test suite validating Stage 1 Observability foundation structures.
    /// </summary>
    public class ObservabilityStage1Tests
    {
        [Fact]
        public void Options_DefaultValues_AreCorrect()
        {
            var telemetry = new TelemetryOptions();
            Assert.True(telemetry.EnableTelemetry);
            Assert.Equal(1.0, telemetry.SamplingRate);
            Assert.Equal(1000, telemetry.BufferSize);

            var metrics = new MetricsOptions();
            Assert.Equal(60, metrics.AggregationWindowSeconds);
            Assert.True(metrics.EnableMovingAverages);

            var tracing = new TracingOptions();
            Assert.Equal(1.0, tracing.SamplingProbability);
            Assert.Equal(10, tracing.MaxTraceDepth);
            Assert.Equal(5000, tracing.RequestTimeoutMilliseconds);

            var perf = new PerformanceOptions();
            Assert.Equal(1000, perf.LatencyWarningThresholdMilliseconds);
            Assert.Equal(512, perf.MemoryLimitMegabytes);

            var diag = new DiagnosticsOptions();
            Assert.Equal(300, diag.ThreadDumpIntervalSeconds);
            Assert.Equal(1024, diag.MemorySnapshotLimitMegabytes);

            var alert = new AlertOptions();
            Assert.Equal(90.0, alert.CpuThresholdPercent);
            Assert.Equal(90.0, alert.MemoryThresholdPercent);
            Assert.Equal(10.0, alert.DiskFreeSpaceThresholdPercent);
            Assert.Equal(300, alert.CooldownPeriodSeconds);

            var dash = new DashboardOptions();
            Assert.Equal(5, dash.RefreshIntervalSeconds);
            Assert.Equal(50, dash.MaxVisibleAlerts);

            var storage = new HistoricalStorageOptions();
            Assert.Equal("Data/historical_metrics.db", storage.DatabasePath);
            Assert.True(storage.UseCompression);
            Assert.Equal(4096, storage.PageSize);

            var mon = new MonitoringOptions();
            Assert.Equal(30, mon.HeartbeatTimeoutSeconds);
            Assert.True(mon.EnableProcessTamperingCheck);

            var retention = new RetentionOptions();
            Assert.Equal(30, retention.RetentionDays);
            Assert.Equal(RetentionPolicyType.Daily, retention.PolicyType);

            var collection = new CollectionOptions();
            Assert.Equal(5, collection.CriticalIntervalSeconds);
            Assert.Equal(15, collection.PerformanceIntervalSeconds);
            Assert.Equal(30, collection.HardwareIntervalSeconds);
            Assert.Equal(60, collection.StorageIntervalSeconds);
            Assert.Equal(300, collection.HistoricalIntervalSeconds);
        }

        [Fact]
        public void Options_Validation_TriggersOnInvalidValues()
        {
            var services = new ServiceCollection();
            var configurationData = new Dictionary<string, string?>
            {
                { "Observability:Telemetry:SamplingRate", "1.5" }, // Invalid (> 1.0)
                { "Observability:Telemetry:BufferSize", "5" }     // Invalid (< 10)
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationData)
                .Build();

            services.AddObservabilityServices(configuration);
            var provider = services.BuildServiceProvider();

            var telemetryOptions = provider.GetRequiredService<IOptions<TelemetryOptions>>();

            var ex = Assert.Throws<OptionsValidationException>(() => _ = telemetryOptions.Value);
            Assert.Contains("TelemetryOptions", ex.Message);
        }

        [Fact]
        public void Options_Validation_SucceedsOnValidValues()
        {
            var services = new ServiceCollection();
            var configurationData = new Dictionary<string, string?>
            {
                { "Observability:Telemetry:SamplingRate", "0.5" },
                { "Observability:Telemetry:BufferSize", "500" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationData)
                .Build();

            services.AddObservabilityServices(configuration);
            var provider = services.BuildServiceProvider();

            var telemetryOptions = provider.GetRequiredService<IOptions<TelemetryOptions>>().Value;
            Assert.Equal(0.5, telemetryOptions.SamplingRate);
            Assert.Equal(500, telemetryOptions.BufferSize);
        }

        [Fact]
        public void Configuration_Binding_PopulatesOptionsCorrectly()
        {
            var services = new ServiceCollection();
            var configurationData = new Dictionary<string, string?>
            {
                { "Observability:Telemetry:EnableTelemetry", "false" },
                { "Observability:Telemetry:SamplingRate", "0.25" },
                { "Observability:Telemetry:BufferSize", "2500" },
                { "Observability:Metrics:AggregationWindowSeconds", "120" },
                { "Observability:Metrics:EnableMovingAverages", "false" },
                { "Observability:Tracing:SamplingProbability", "0.1" },
                { "Observability:Tracing:MaxTraceDepth", "5" },
                { "Observability:Tracing:RequestTimeoutMilliseconds", "3000" },
                { "Observability:Performance:LatencyWarningThresholdMilliseconds", "500" },
                { "Observability:Performance:MemoryLimitMegabytes", "256" },
                { "Observability:Diagnostics:ThreadDumpIntervalSeconds", "60" },
                { "Observability:Diagnostics:MemorySnapshotLimitMegabytes", "512" },
                { "Observability:Alerts:CpuThresholdPercent", "80" },
                { "Observability:Alerts:MemoryThresholdPercent", "85" },
                { "Observability:Alerts:DiskFreeSpaceThresholdPercent", "15" },
                { "Observability:Alerts:CooldownPeriodSeconds", "600" },
                { "Observability:Dashboard:RefreshIntervalSeconds", "10" },
                { "Observability:Dashboard:MaxVisibleAlerts", "100" },
                { "Observability:HistoricalStorage:DatabasePath", "CustomPath/historical.db" },
                { "Observability:HistoricalStorage:UseCompression", "false" },
                { "Observability:HistoricalStorage:PageSize", "8192" },
                { "Observability:Monitoring:HeartbeatTimeoutSeconds", "45" },
                { "Observability:Monitoring:EnableProcessTamperingCheck", "false" },
                { "Observability:Retention:RetentionDays", "60" },
                { "Observability:Retention:PolicyType", "Weekly" },
                { "Observability:Collection:CriticalIntervalSeconds", "10" },
                { "Observability:Collection:PerformanceIntervalSeconds", "30" },
                { "Observability:Collection:HardwareIntervalSeconds", "60" },
                { "Observability:Collection:StorageIntervalSeconds", "120" },
                { "Observability:Collection:HistoricalIntervalSeconds", "600" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationData)
                .Build();

            services.AddObservabilityServices(configuration);
            var provider = services.BuildServiceProvider();

            Assert.False(provider.GetRequiredService<IOptions<TelemetryOptions>>().Value.EnableTelemetry);
            Assert.Equal(0.25, provider.GetRequiredService<IOptions<TelemetryOptions>>().Value.SamplingRate);
            Assert.Equal(2500, provider.GetRequiredService<IOptions<TelemetryOptions>>().Value.BufferSize);

            Assert.Equal(120, provider.GetRequiredService<IOptions<MetricsOptions>>().Value.AggregationWindowSeconds);
            Assert.False(provider.GetRequiredService<IOptions<MetricsOptions>>().Value.EnableMovingAverages);

            Assert.Equal(0.1, provider.GetRequiredService<IOptions<TracingOptions>>().Value.SamplingProbability);
            Assert.Equal(5, provider.GetRequiredService<IOptions<TracingOptions>>().Value.MaxTraceDepth);
            Assert.Equal(3000, provider.GetRequiredService<IOptions<TracingOptions>>().Value.RequestTimeoutMilliseconds);

            Assert.Equal(500, provider.GetRequiredService<IOptions<PerformanceOptions>>().Value.LatencyWarningThresholdMilliseconds);
            Assert.Equal(256, provider.GetRequiredService<IOptions<PerformanceOptions>>().Value.MemoryLimitMegabytes);

            Assert.Equal(60, provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value.ThreadDumpIntervalSeconds);
            Assert.Equal(512, provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value.MemorySnapshotLimitMegabytes);

            Assert.Equal(80.0, provider.GetRequiredService<IOptions<AlertOptions>>().Value.CpuThresholdPercent);
            Assert.Equal(85.0, provider.GetRequiredService<IOptions<AlertOptions>>().Value.MemoryThresholdPercent);
            Assert.Equal(15.0, provider.GetRequiredService<IOptions<AlertOptions>>().Value.DiskFreeSpaceThresholdPercent);
            Assert.Equal(600, provider.GetRequiredService<IOptions<AlertOptions>>().Value.CooldownPeriodSeconds);

            Assert.Equal(10, provider.GetRequiredService<IOptions<DashboardOptions>>().Value.RefreshIntervalSeconds);
            Assert.Equal(100, provider.GetRequiredService<IOptions<DashboardOptions>>().Value.MaxVisibleAlerts);

            Assert.Equal("CustomPath/historical.db", provider.GetRequiredService<IOptions<HistoricalStorageOptions>>().Value.DatabasePath);
            Assert.False(provider.GetRequiredService<IOptions<HistoricalStorageOptions>>().Value.UseCompression);
            Assert.Equal(8192, provider.GetRequiredService<IOptions<HistoricalStorageOptions>>().Value.PageSize);

            Assert.Equal(45, provider.GetRequiredService<IOptions<MonitoringOptions>>().Value.HeartbeatTimeoutSeconds);
            Assert.False(provider.GetRequiredService<IOptions<MonitoringOptions>>().Value.EnableProcessTamperingCheck);

            Assert.Equal(60, provider.GetRequiredService<IOptions<RetentionOptions>>().Value.RetentionDays);
            Assert.Equal(RetentionPolicyType.Weekly, provider.GetRequiredService<IOptions<RetentionOptions>>().Value.PolicyType);

            Assert.Equal(10, provider.GetRequiredService<IOptions<CollectionOptions>>().Value.CriticalIntervalSeconds);
            Assert.Equal(30, provider.GetRequiredService<IOptions<CollectionOptions>>().Value.PerformanceIntervalSeconds);
            Assert.Equal(60, provider.GetRequiredService<IOptions<CollectionOptions>>().Value.HardwareIntervalSeconds);
            Assert.Equal(120, provider.GetRequiredService<IOptions<CollectionOptions>>().Value.StorageIntervalSeconds);
            Assert.Equal(600, provider.GetRequiredService<IOptions<CollectionOptions>>().Value.HistoricalIntervalSeconds);
        }

        [Fact]
        public void ValueObjects_AreImmutableAndCorrect()
        {
            var traceId1 = new TraceId();
            var traceId2 = new TraceId(traceId1.Value);
            Assert.Equal(traceId1, traceId2);

            var traceIdString = (string)traceId1;
            Assert.Equal(traceId1.Value, traceIdString);

            var correlationId1 = new CorrelationId();
            var correlationId2 = new CorrelationId(correlationId1.Value);
            Assert.Equal(correlationId1, correlationId2);

            var correlationIdString = (string)correlationId1;
            Assert.Equal(correlationId1.Value, correlationIdString);
        }

        [Fact]
        public void Models_Serialization_IsSuccessfulAndPreservesTypes()
        {
            var record = new TelemetryRecord
            {
                Timestamp = DateTime.UtcNow,
                MetricName = "test_metric",
                Category = MetricCategory.Cpu,
                Value = 75.5,
                Unit = MetricUnit.Percent,
                Source = "test_source",
                Severity = MetricSeverity.Warning,
                Tags = new Dictionary<string, string> { { "env", "test" } },
                CorrelationId = new CorrelationId()
            };

            var json = JsonSerializer.Serialize(record);
            var deserialized = JsonSerializer.Deserialize<TelemetryRecord>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(record.MetricName, deserialized.MetricName);
            Assert.Equal(record.Category, deserialized.Category);
            Assert.Equal(record.Value, deserialized.Value);
            Assert.Equal(record.Unit, deserialized.Unit);
            Assert.Equal(record.Source, deserialized.Source);
            Assert.Equal(record.Severity, deserialized.Severity);
            Assert.Equal(record.Tags["env"], deserialized.Tags["env"]);
            Assert.Equal(record.CorrelationId, deserialized.CorrelationId);
        }

        [Fact]
        public void Constants_DoNotHaveMagicStringsOrDuplicates()
        {
            Assert.Equal("system.cpu.usage", ObservabilityConstants.MetricNames.CpuUsage);
            Assert.Equal("Observability:Telemetry", ObservabilityConstants.ConfigurationKeys.Telemetry);
            Assert.Equal(5, (int)CollectionInterval.Critical);
            Assert.Equal(15, ObservabilityConstants.DefaultIntervals.Performance.TotalSeconds);
        }

        [Fact]
        public void Exceptions_InstantiateAndBehaveCorrectly()
        {
            var message = "Error occurred";
            var inner = new InvalidOperationException("Inner failure");

            var telemetryEx = new TelemetryException(message, inner);
            Assert.Equal(message, telemetryEx.Message);
            Assert.Equal(inner, telemetryEx.InnerException);

            var metricsEx = new MetricsException(message, inner);
            Assert.Equal(message, metricsEx.Message);
            Assert.Equal(inner, metricsEx.InnerException);

            var tracingEx = new TracingException(message, inner);
            Assert.Equal(message, tracingEx.Message);
            Assert.Equal(inner, tracingEx.InnerException);

            var diagEx = new DiagnosticsException(message, inner);
            Assert.Equal(message, diagEx.Message);
            Assert.Equal(inner, diagEx.InnerException);

            var alertEx = new AlertException(message, inner);
            Assert.Equal(message, alertEx.Message);
            Assert.Equal(inner, alertEx.InnerException);

            var dashEx = new DashboardException(message, inner);
            Assert.Equal(message, dashEx.Message);
            Assert.Equal(inner, dashEx.InnerException);

            var monEx = new MonitoringException(message, inner);
            Assert.Equal(message, monEx.Message);
            Assert.Equal(inner, monEx.InnerException);

            var storageEx = new HistoricalStorageException(message, inner);
            Assert.Equal(message, storageEx.Message);
            Assert.Equal(inner, storageEx.InnerException);
        }

        [Fact]
        public void ResultModels_CreateSuccessAndFailureCorrectly()
        {
            var opResult = OperationResult.Success("Ok");
            Assert.True(opResult.IsSuccess);
            Assert.Equal("Ok", opResult.Message);

            var opFailed = OperationResult.Failure("Error");
            Assert.False(opFailed.IsSuccess);
            Assert.Equal("Error", opFailed.Message);

            var diag = DiagnosticResult.Success(new DiagnosticReport(), "Success");
            Assert.True(diag.IsSuccess);
            Assert.NotNull(diag.Report);

            var telemetry = TelemetryResult.Failure("Failed transmission");
            Assert.False(telemetry.IsSuccess);
            Assert.Equal("Failed transmission", telemetry.Message);
        }
    }
}
