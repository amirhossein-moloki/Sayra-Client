using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Diagnostics.Configuration;
using Sayra.Client.Diagnostics.Interfaces;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Providers;
using Sayra.Client.Diagnostics.Services;

namespace Sayra.Client.Diagnostics.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDiagnosticsServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register strongly-typed options
            services.Configure<DiagnosticsOptions>(configuration.GetSection(DiagnosticsOptions.SectionName));

            // Register Providers
            services.AddSingleton<IWmiProvider, WmiProvider>();
            services.AddSingleton<IPerformanceCounterProvider, PerformanceCounterProvider>();
            services.AddSingleton<IDisplayProvider, DisplayProvider>();
            services.AddSingleton<INetworkProvider, NetworkProvider>();
            services.AddSingleton<IGpuProvider, GpuProvider>();

            // Register Core Services
            services.AddSingleton<IHardwareCacheService, HardwareCacheService>();
            services.AddSingleton<IHardwareSpecificationService, HardwareSpecificationService>();
            services.AddSingleton<IHardwareTelemetryService, HardwareTelemetryService>();
            services.AddSingleton<IHardwareValidationService, HardwareValidationService>();

            // Register Monitoring Service as Singleton and HostedService
            services.AddSingleton<HardwareMonitoringService>();
            services.AddSingleton<IHardwareMonitoringService>(sp => sp.GetRequiredService<HardwareMonitoringService>());
            services.AddHostedService(sp => sp.GetRequiredService<HardwareMonitoringService>());

            return services;
        }
    }
}
