using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Fleet.Monitoring.Collectors;
using Sayra.Client.Shared.Fleet.Monitoring.Interfaces;
using Sayra.Client.Shared.Fleet.Monitoring.Services;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering Phase 9 Stage 4 Enterprise Live Monitoring Engine.
    /// </summary>
    public static class LiveMonitoringServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all backend Live Monitoring Engine services, caches, coordinators, and collectors.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddLiveMonitoring(this IServiceCollection services)
        {
            // Register Cache
            services.AddSingleton<IMonitoringCache, MonitoringCache>();

            // Register Engines
            services.AddSingleton<ISamplingEngine, SamplingEngine>();
            services.AddSingleton<ISnapshotEngine, SnapshotEngine>();
            services.AddSingleton<IAggregationEngine, AggregationEngine>();
            services.AddSingleton<IThresholdEvaluator, ThresholdEvaluator>();

            // Register Pipeline
            services.AddSingleton<IMonitoringPipeline, MonitoringPipeline>();

            // Register Master Poller
            services.AddSingleton<IPollingEngine, PollingEngine>();

            // Register Main Coordinators
            services.AddSingleton<LiveMonitoringService>();
            services.AddSingleton<ILiveMonitoringService>(sp => sp.GetRequiredService<LiveMonitoringService>());
            services.AddSingleton<ITelemetryAggregator>(sp => sp.GetRequiredService<LiveMonitoringService>());

            services.AddSingleton<IMonitoringScheduler, MonitoringScheduler>();
            services.AddSingleton<ILiveMonitoringQueryService, LiveMonitoringQueryService>();
            services.AddSingleton<ILiveMonitoringSecurityService, LiveMonitoringSecurityService>();

            // Register All 10 Pluggable Metric Collectors
            services.AddSingleton<ILiveMetricCollector, CpuMetricCollector>();
            services.AddSingleton<ILiveMetricCollector, MemoryMetricCollector>();
            services.AddSingleton<ILiveMetricCollector, DiskMetricCollector>();
            services.AddSingleton<ILiveMetricCollector, GpuMetricCollector>();
            services.AddSingleton<ILiveMetricCollector, NetworkMetricCollector>();
            services.AddSingleton<ILiveMetricCollector, NetworkDiagnosticsCollector>();
            services.AddSingleton<ILiveMetricCollector, SessionMetricCollector>();
            services.AddSingleton<ILiveMetricCollector, ProcessMetricCollector>();
            services.AddSingleton<ILiveMetricCollector, ServicesMetricCollector>();
            services.AddSingleton<ILiveMetricCollector, MotherboardMetricCollector>();

            return services;
        }
    }
}
