using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Launcher.Services;
using Sayra.Client.Launcher.Validation;

namespace Sayra.Client.Launcher
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLauncherServices(this IServiceCollection services)
        {
            services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();
            services.AddSingleton<ILauncherRecoveryService, LauncherRecoveryService>();
            services.AddSingleton<ILicenseValidator, LicenseValidator>();
            services.AddSingleton<IGameLauncherService, GameLauncherService>();

            return services;
        }
    }
}
