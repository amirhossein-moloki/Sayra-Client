using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Constants;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Telemetry;
using Sayra.Client.Shared.Telemetry.Collectors.Hardware;
using Sayra.Client.Shared.Telemetry.Collectors.Runtime;
using Sayra.Client.Shared.Telemetry.Metrics;
using Sayra.Client.Shared.Telemetry.Tracing;
using Sayra.Client.Shared.Telemetry.Performance;
using Sayra.Client.Shared.Telemetry.Diagnostics;
using Sayra.Client.Shared.Telemetry.Diagnostics.Modules;
using Sayra.Client.Shared.Telemetry.Alerts;
using Sayra.Client.Shared.Telemetry.Alerts.Evaluators;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Service collection extension methods to configure and register the enterprise observability platform.
    /// </summary>
    public static class ObservabilityServiceCollectionExtensions
    {
        /// <summary>
        /// Registers and binds all strongly typed observability options and services.
        /// </summary>
        /// <param name="services">The service collection instance.</param>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddObservabilityServices(this IServiceCollection services, IConfiguration configuration)
        {
            // --- Options Binding & Validation ---

            // Bind and register TelemetryOptions
            services.AddOptions<TelemetryOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.Telemetry))
                .Validate(o => o.SamplingRate >= 0.0 && o.SamplingRate <= 1.0 && o.BufferSize >= 10 && o.BufferSize <= 10000,
                    "TelemetryOptions: SamplingRate must be between 0.0 and 1.0, and BufferSize must be between 10 and 10000.");

            // Bind and register MetricsOptions
            services.AddOptions<MetricsOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.Metrics))
                .Validate(o => o.AggregationWindowSeconds >= 1 && o.AggregationWindowSeconds <= 3600,
                    "MetricsOptions: AggregationWindowSeconds must be between 1 and 3600.");

            // Bind and register TracingOptions
            services.AddOptions<TracingOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.Tracing))
                .Validate(o => o.SamplingProbability >= 0.0 && o.SamplingProbability <= 1.0 && o.MaxTraceDepth >= 1 && o.MaxTraceDepth <= 100 && o.RequestTimeoutMilliseconds >= 100 && o.RequestTimeoutMilliseconds <= 60000,
                    "TracingOptions: SamplingProbability must be [0.0, 1.0], MaxTraceDepth [1, 100], and RequestTimeoutMilliseconds [100, 60000].");

            // Bind and register PerformanceOptions
            services.AddOptions<PerformanceOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.Performance))
                .Validate(o => o.LatencyWarningThresholdMilliseconds >= 10 && o.LatencyWarningThresholdMilliseconds <= 10000 && o.MemoryLimitMegabytes >= 10 && o.MemoryLimitMegabytes <= 2048,
                    "PerformanceOptions: LatencyWarningThresholdMilliseconds must be [10, 10000] and MemoryLimitMegabytes [10, 2048].");

            // Bind and register DiagnosticsOptions
            services.AddOptions<DiagnosticsOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.Diagnostics))
                .Validate(o => o.ThreadDumpIntervalSeconds >= 10 && o.ThreadDumpIntervalSeconds <= 86400 && o.MemorySnapshotLimitMegabytes >= 10 && o.MemorySnapshotLimitMegabytes <= 4096,
                    "DiagnosticsOptions: ThreadDumpIntervalSeconds must be [10, 86400] and MemorySnapshotLimitMegabytes [10, 4096].");

            // Bind and register AlertOptions
            services.AddOptions<AlertOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.Alerts))
                .Validate(o => o.CpuThresholdPercent >= 1.0 && o.CpuThresholdPercent <= 100.0 && o.MemoryThresholdPercent >= 1.0 && o.MemoryThresholdPercent <= 100.0 && o.DiskFreeSpaceThresholdPercent >= 1.0 && o.DiskFreeSpaceThresholdPercent <= 100.0 && o.CooldownPeriodSeconds >= 1 && o.CooldownPeriodSeconds <= 3600,
                    "AlertOptions: Thresholds must be between 1.0% and 100.0%, CooldownPeriodSeconds between 1 and 3600.");

            // Bind and register DashboardOptions
            services.AddOptions<DashboardOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.Dashboard))
                .Validate(o => o.RefreshIntervalSeconds >= 1 && o.RefreshIntervalSeconds <= 300 && o.MaxVisibleAlerts >= 1 && o.MaxVisibleAlerts <= 500,
                    "DashboardOptions: RefreshIntervalSeconds must be [1, 300] and MaxVisibleAlerts [1, 500].");

            // Bind and register HistoricalStorageOptions
            services.AddOptions<HistoricalStorageOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.HistoricalStorage))
                .Validate(o => !string.IsNullOrWhiteSpace(o.DatabasePath) && o.PageSize >= 512 && o.PageSize <= 65536,
                    "HistoricalStorageOptions: DatabasePath cannot be empty and PageSize must be [512, 65536].");

            // Bind and register MonitoringOptions
            services.AddOptions<MonitoringOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.Monitoring))
                .Validate(o => o.HeartbeatTimeoutSeconds >= 1 && o.HeartbeatTimeoutSeconds <= 120,
                    "MonitoringOptions: HeartbeatTimeoutSeconds must be between 1 and 120.");

            // Bind and register RetentionOptions
            services.AddOptions<RetentionOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.Retention))
                .Validate(o => o.RetentionDays >= 1 && o.RetentionDays <= 365,
                    "RetentionOptions: RetentionDays must be between 1 and 365.");

            // Bind and register CollectionOptions
            services.AddOptions<CollectionOptions>()
                .Bind(configuration.GetSection(ObservabilityConstants.ConfigurationKeys.Collection))
                .Validate(o => o.CriticalIntervalSeconds >= 1 && o.CriticalIntervalSeconds <= 60 && o.PerformanceIntervalSeconds >= 1 && o.PerformanceIntervalSeconds <= 300 && o.HardwareIntervalSeconds >= 1 && o.HardwareIntervalSeconds <= 600 && o.StorageIntervalSeconds >= 1 && o.StorageIntervalSeconds <= 3600 && o.HistoricalIntervalSeconds >= 1 && o.HistoricalIntervalSeconds <= 86400,
                    "CollectionOptions: Intervals must be within valid range bounds.");


            // --- Service Registrations ---

            // Register Hardware Sensor Provider
            services.AddSingleton<IHardwareSensorProvider, HardwareSensorProvider>();

            // Register Telemetry Pipeline
            services.AddSingleton<TelemetryPipeline>();

            // Register Telemetry Service & Metrics Collector
            services.AddSingleton<TelemetryService>();
            services.AddSingleton<ITelemetryService>(sp => sp.GetRequiredService<TelemetryService>());
            services.AddSingleton<IMetricsCollector, MetricsCollector>();

            // Register Tracing Service
            services.AddSingleton<ITracingService, TracingService>();

            // --- Performance Monitoring Services (Phase 8 Stage 5) ---
            // Lifetime Decision: IPerformanceMonitor and all specialized wrappers are registered
            // as Singletons because they must maintain thread-safe, system-wide state, historical average buffers,
            // active async counters, speed measurements, and cache stats across the entire application lifetime.
            services.AddSingleton<PerformanceMonitor>();
            services.AddSingleton<IPerformanceMonitor>(sp => sp.GetRequiredService<PerformanceMonitor>());
            services.AddSingleton<DatabasePerformanceMonitor>();
            services.AddSingleton<IpcPerformanceMonitor>();
            services.AddSingleton<NetworkPerformanceMonitor>();
            services.AddSingleton<CachePerformanceMonitor>();
            services.AddSingleton<RuntimePerformanceMonitor>();
            services.AddSingleton<StartupPerformanceMonitor>();

            // Register Metrics Aggregator Strategies
            services.AddSingleton<IMetricAggregatorStrategy, CounterAggregatorStrategy>();
            services.AddSingleton<IMetricAggregatorStrategy, GaugeAggregatorStrategy>();
            services.AddSingleton<IMetricAggregatorStrategy, HistogramAggregatorStrategy>();
            services.AddSingleton<IMetricAggregatorStrategy, TimerAggregatorStrategy>();
            services.AddSingleton<IMetricAggregatorStrategy, RateAggregatorStrategy>();

            // Register Metrics Aggregator Engine
            services.AddSingleton<IMetricsAggregator, MetricsAggregator>();

            // --- Diagnostics Platform Services (Phase 8 Stage 6) ---
            services.AddSingleton<IDiagnosticsRecommendationEngine, DiagnosticsRecommendationEngine>();
            services.AddSingleton<Sayra.Client.Shared.Interfaces.Telemetry.IDiagnosticsEngine, DiagnosticsEngine>();

            // Register all 16 Diagnostic Modules as IDiagnosticModule
            services.AddSingleton<IDiagnosticModule, HardwareDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, OsDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, RuntimeDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, NetworkDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, DatabaseDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, StorageDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, SecurityDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, PluginsDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, ConfigurationDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, IpcDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, SynchronizationDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, NotificationsDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, DownloadsDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, UpdatesDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, OverlayDiagnosticModule>();
            services.AddSingleton<IDiagnosticModule, WatchdogDiagnosticModule>();

            // Register 16 Collectors as IExtendedTelemetryCollector
            services.AddSingleton<IExtendedTelemetryCollector, CpuCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, MemoryCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, GpuCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, DiskCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, NetworkCollector>();

            services.AddSingleton<IExtendedTelemetryCollector, ProcessesCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, WindowsSessionsCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, PluginsCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, WatchdogCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, PolicyCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, DownloadsCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, UpdatesCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, IpcCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, SyncCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, NotificationCollector>();
            services.AddSingleton<IExtendedTelemetryCollector, OverlayCollector>();

            // --- Alert Engine Platform Services (Phase 8 Stage 7) ---
            services.AddSingleton<IAlertDiagnosticsCache, AlertDiagnosticsCache>();
            services.AddSingleton<IAlertPolicyProvider, AlertPolicyProvider>();
            services.AddSingleton<IAlertRuleProvider, AlertRuleProvider>();
            services.AddSingleton<IAlertDeduplicationProvider, AlertDeduplicationProvider>();
            services.AddSingleton<IAlertRecoveryProvider, AlertRecoveryProvider>();
            services.AddSingleton<IAlertSuppressionProvider, AlertSuppressionProvider>();
            services.AddSingleton<IAlertEscalationProvider, AlertEscalationProvider>();
            services.AddSingleton<IAlertEngine, AlertEngine>();

            // Register the 13 required alert rule evaluators
            services.AddSingleton<IAlertRuleEvaluator, CpuThresholdRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, MemoryThresholdRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, DiskUsageRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, NetworkFailuresRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, DatabaseFailuresRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, IpcFailuresRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, DownloadFailuresRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, UpdateFailuresRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, PluginFailuresRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, SecurityFailuresRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, PolicyViolationsRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, RuntimeFailuresRuleEvaluator>();
            services.AddSingleton<IAlertRuleEvaluator, ConfigurationFailuresRuleEvaluator>();

            return services;
        }
    }
}
