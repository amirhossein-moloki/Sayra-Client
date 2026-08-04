using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.RemoteCommands.Commands;
using Sayra.Client.Shared.Fleet.RemoteCommands.Handlers;
using Sayra.Client.Shared.Fleet.RemoteCommands.History;
using Sayra.Client.Shared.Fleet.RemoteCommands.Pipeline;
using Sayra.Client.Shared.Fleet.RemoteCommands.Queues;
using Sayra.Client.Shared.Fleet.RemoteCommands.Security;
using Sayra.Client.Shared.Fleet.RemoteCommands.Validation;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.DependencyInjection
{
    /// <summary>
    /// Service collection extension methods to register the Phase 9 Stage 3 Remote Command Framework.
    /// </summary>
    public static class RemoteCommandFrameworkServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all repositories, queues, validators, security services, pipeline middlewares, and handlers for the Remote Command Framework.
        /// </summary>
        public static IServiceCollection AddRemoteCommandFramework(this IServiceCollection services)
        {
            // 1. Validator & Security services
            services.TryAddSingleton<IRemoteCommandValidator, RemoteCommandValidator>();
            services.TryAddSingleton<IRemoteCommandAuthorizationService, RemoteCommandAuthorizationService>();

            // 2. Persistent History Repository
            services.TryAddSingleton<IRemoteCommandHistoryRepository, RemoteCommandHistoryRepository>();

            // 3. Queue Services
            services.TryAddSingleton<RemoteCommandQueue>();
            services.TryAddSingleton<IRemoteCommandQueue>(sp => sp.GetRequiredService<RemoteCommandQueue>());
            services.TryAddSingleton<IEnterpriseCommandQueue>(sp => sp.GetRequiredService<RemoteCommandQueue>());

            // 4. Strongly-Typed Command Handlers
            services.TryAddTransient<IRemoteCommandHandler<RestartMachineCommand>, RestartMachineCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<ShutdownMachineCommand>, ShutdownMachineCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<RestartWindowsServiceCommand>, RestartWindowsServiceCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<RestartSayraServiceCommand>, RestartSayraServiceCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<RestartWorkerCommand>, RestartWorkerCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<RestartIpcCommand>, RestartIpcCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<RestartOverlayCommand>, RestartOverlayCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<LockWorkstationCommand>, LockWorkstationCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<UnlockWorkstationCommand>, UnlockWorkstationCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<ReloadConfigurationCommand>, ReloadConfigurationCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<RefreshConfigurationCommand>, RefreshConfigurationCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<SyncConfigurationCommand>, SyncConfigurationCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<FlushCacheCommand>, FlushCacheCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<RefreshTelemetryCommand>, RefreshTelemetryCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<RunHealthCheckCommand>, RunHealthCheckCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<StartMaintenanceCommand>, StartMaintenanceCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<StopMaintenanceCommand>, StopMaintenanceCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<ClearTemporaryDataCommand>, ClearTemporaryDataCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<ClearDownloadsCommand>, ClearDownloadsCommandHandler>();
            services.TryAddTransient<IRemoteCommandHandler<CustomAdminCommand>, CustomAdminCommandHandler>();

            // 5. Handlers Registry (Dynamic resolution through DI provider to avoid captive dependencies)
            services.TryAddSingleton<IRemoteCommandHandlerRegistry>(sp =>
            {
                var registry = new RemoteCommandHandlerRegistry();

                registry.Register(RemoteCommandActions.RestartMachine, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<RestartMachineCommand>>().HandleAsync((RestartMachineCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.ShutdownMachine, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<ShutdownMachineCommand>>().HandleAsync((ShutdownMachineCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.RestartWindowsService, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<RestartWindowsServiceCommand>>().HandleAsync((RestartWindowsServiceCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.RestartSayraService, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<RestartSayraServiceCommand>>().HandleAsync((RestartSayraServiceCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.RestartWorker, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<RestartWorkerCommand>>().HandleAsync((RestartWorkerCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.RestartIpc, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<RestartIpcCommand>>().HandleAsync((RestartIpcCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.RestartOverlay, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<RestartOverlayCommand>>().HandleAsync((RestartOverlayCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.LockWorkstation, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<LockWorkstationCommand>>().HandleAsync((LockWorkstationCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.UnlockWorkstation, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<UnlockWorkstationCommand>>().HandleAsync((UnlockWorkstationCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.ReloadConfiguration, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<ReloadConfigurationCommand>>().HandleAsync((ReloadConfigurationCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.RefreshConfiguration, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<RefreshConfigurationCommand>>().HandleAsync((RefreshConfigurationCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.SyncConfiguration, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<SyncConfigurationCommand>>().HandleAsync((SyncConfigurationCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.FlushCache, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<FlushCacheCommand>>().HandleAsync((FlushCacheCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.RefreshTelemetry, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<RefreshTelemetryCommand>>().HandleAsync((RefreshTelemetryCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.RunHealthCheck, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<RunHealthCheckCommand>>().HandleAsync((RunHealthCheckCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.StartMaintenance, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<StartMaintenanceCommand>>().HandleAsync((StartMaintenanceCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.StopMaintenance, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<StopMaintenanceCommand>>().HandleAsync((StopMaintenanceCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.ClearTemporaryData, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<ClearTemporaryDataCommand>>().HandleAsync((ClearTemporaryDataCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.ClearDownloads, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<ClearDownloadsCommand>>().HandleAsync((ClearDownloadsCommand)RemoteCommandFactory.Map(cmd), ct));
                registry.Register(RemoteCommandActions.CustomAdminCommand, (cmd, ct) => sp.GetRequiredService<IRemoteCommandHandler<CustomAdminCommand>>().HandleAsync((CustomAdminCommand)RemoteCommandFactory.Map(cmd), ct));

                return registry;
            });

            // 6. Pipeline Middlewares (evaluated in order of registration)
            services.AddSingleton<IRemoteCommandMiddleware, ExceptionMiddleware>();
            services.AddSingleton<IRemoteCommandMiddleware, LoggingMiddleware>();
            services.AddSingleton<IRemoteCommandMiddleware, TelemetryMiddleware>();
            services.AddSingleton<IRemoteCommandMiddleware, AuditMiddleware>();
            services.AddSingleton<IRemoteCommandMiddleware, ValidationMiddleware>();
            services.AddSingleton<IRemoteCommandMiddleware, AuthorizationMiddleware>();
            services.AddSingleton<IRemoteCommandMiddleware, TimeoutMiddleware>();
            services.AddSingleton<IRemoteCommandMiddleware, RetryMiddleware>();

            // 7. Dispatcher & Services
            services.TryAddSingleton<RemoteCommandDispatcher>();
            services.TryAddSingleton<IRemoteCommandDispatcher>(sp => sp.GetRequiredService<RemoteCommandDispatcher>());
            services.TryAddSingleton<IRemoteCommandService>(sp => sp.GetRequiredService<RemoteCommandDispatcher>());

            return services;
        }
    }
}
