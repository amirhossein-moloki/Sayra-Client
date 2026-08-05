using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Fleet.RemoteAssistance;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.DependencyInjection
{
    /// <summary>
    /// Service collection extensions for registering Stage 9 Remote Assistance Framework dependencies.
    /// </summary>
    public static class RemoteAssistanceServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all core remote assistance support, security, console, and log streaming services.
        /// </summary>
        public static IServiceCollection AddRemoteAssistance(this IServiceCollection services)
        {
            // Session Coordinator (Timer-based timeout, tracking session active lists)
            services.AddSingleton<RemoteSessionCoordinator>();

            // Security Services & Authorization Workflows
            services.AddSingleton<RemoteSessionSecurity>();

            // Streaming Abstractions and Services
            services.AddSingleton<IRemoteDesktopProvider, RemoteDesktopProvider>();
            services.AddSingleton<IRemoteConsoleService, RemoteConsoleService>();
            services.AddSingleton<IRemoteLogStreamService, RemoteLogStreamService>();
            services.AddSingleton<IRemoteEventStreamService, RemoteEventStreamService>();

            // Session Managers & Core Engine Schedulers
            services.AddSingleton<IRemoteSupportService, RemoteSupportEngine>();
            services.AddSingleton<IRemoteSessionManager, RemoteSessionManager>();

            return services;
        }
    }
}
