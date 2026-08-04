using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Fleet.Diagnostics.Interfaces;
using Sayra.Client.Shared.Fleet.Diagnostics.Services;
using Sayra.Client.Shared.Fleet.Diagnostics.Services.Collectors;

namespace Sayra.Client.Shared.DependencyInjection
{
    /// <summary>
    /// Service collection extension methods to register Remote Diagnostics subsystem dependencies.
    /// </summary>
    public static class RemoteDiagnosticsServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all services, storage, collectors, analyzers, and coordinators for the Enterprise Remote Diagnostics Engine.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddRemoteDiagnostics(this IServiceCollection services)
        {
            // Core Storage and Registry
            services.AddSingleton<IDiagnosticStorage, DiagnosticStorage>();
            services.AddSingleton<IDiagnosticReportRegistry, DiagnosticReportRegistry>();

            // Package Builder & Analyzer
            services.AddTransient<IDiagnosticPackageBuilder, DiagnosticPackageBuilder>();
            services.AddTransient<DiagnosticAnalyzer>();

            // Pipeline & Processors
            services.AddTransient<DiagnosticsPipeline>();
            services.AddTransient<DiagnosticsResultProcessor>();

            // Coordinator & Scheduler
            services.AddSingleton<DiagnosticsCoordinator>();
            services.AddSingleton<DiagnosticsScheduler>();

            // Remote Diagnostics Service Engine
            services.AddTransient<IRemoteDiagnosticsService, RemoteDiagnosticsEngine>();

            // Register modular collectors individually
            services.AddTransient<IHealthDiagnosticCollector, HealthDiagnosticCollector>();
            services.AddTransient<IPerformanceDiagnosticCollector, PerformanceDiagnosticCollector>();
            services.AddTransient<ICrashDiagnosticCollector, CrashDiagnosticCollector>();
            services.AddTransient<IConfigurationDiagnosticCollector, ConfigurationDiagnosticCollector>();
            services.AddTransient<ISecurityDiagnosticCollector, SecurityDiagnosticCollector>();
            services.AddTransient<IDatabaseDiagnosticCollector, DatabaseDiagnosticCollector>();
            services.AddTransient<IPluginDiagnosticCollector, PluginDiagnosticCollector>();
            services.AddTransient<INetworkDiagnosticCollector, NetworkDiagnosticCollector>();
            services.AddTransient<IStorageDiagnosticCollector, StorageDiagnosticCollector>();

            // Register modular collectors as IDiagnosticCollector for the pipeline
            services.AddTransient<IDiagnosticCollector, HealthDiagnosticCollector>();
            services.AddTransient<IDiagnosticCollector, PerformanceDiagnosticCollector>();
            services.AddTransient<IDiagnosticCollector, CrashDiagnosticCollector>();
            services.AddTransient<IDiagnosticCollector, ConfigurationDiagnosticCollector>();
            services.AddTransient<IDiagnosticCollector, SecurityDiagnosticCollector>();
            services.AddTransient<IDiagnosticCollector, DatabaseDiagnosticCollector>();
            services.AddTransient<IDiagnosticCollector, PluginDiagnosticCollector>();
            services.AddTransient<IDiagnosticCollector, NetworkDiagnosticCollector>();
            services.AddTransient<IDiagnosticCollector, StorageDiagnosticCollector>();

            return services;
        }
    }
}
