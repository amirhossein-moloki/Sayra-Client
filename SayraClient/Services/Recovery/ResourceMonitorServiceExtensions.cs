using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Interfaces.Recovery.Providers;
using Sayra.Client.Shared.Models.Recovery;
using SayraClient.Services.Recovery.Providers.Windows;

namespace SayraClient.Services.Recovery
{
    /// <summary>
    /// Dependency Injection configuration for the Enterprise Resource Monitoring Engine.
    /// </summary>
    public static class ResourceMonitorServiceExtensions
    {
        /// <summary>
        /// Registers all resource monitoring services, options, and providers.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration object.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddResourceMonitoringServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 1. Register Configurable Options
            services.Configure<ResourceMonitorOptions>(
                configuration.GetSection("Recovery:ResourceMonitor"));

            // 2. Register Platform-Specific / Fallback Providers
            services.AddSingleton<ICpuMetricsProvider, WindowsCpuMetricsProvider>();
            services.AddSingleton<IMemoryMetricsProvider, WindowsMemoryMetricsProvider>();
            services.AddSingleton<IDiskMetricsProvider, WindowsDiskMetricsProvider>();
            services.AddSingleton<INetworkMetricsProvider, WindowsNetworkMetricsProvider>();
            services.AddSingleton<IGpuMetricsProvider, WindowsGpuMetricsProvider>();
            services.AddSingleton<IProcessMetricsProvider, WindowsProcessMetricsProvider>();

            // 3. Register the Resource Monitor itself
            services.AddSingleton<IResourceMonitor, ResourceMonitor>();

            return services;
        }
    }
}
