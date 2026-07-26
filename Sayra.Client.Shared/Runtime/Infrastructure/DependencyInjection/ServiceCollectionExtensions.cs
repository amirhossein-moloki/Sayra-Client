using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Application.Services;
using Sayra.Client.Shared.Runtime.Infrastructure.Persistence;

namespace Sayra.Client.Shared.Runtime.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRuntimeServices(this IServiceCollection services)
        {
            services.AddSingleton<IRuntimeEventPublisher, RuntimeEventPublisher>();
            services.AddSingleton<IRuntimeStateManager, RuntimeStateManager>();
            services.AddSingleton<IRuntimeContextProvider, RuntimeContextProvider>();
            services.AddRuntimeSessionServices();
            return services;
        }

        public static IServiceCollection AddRuntimeSessionServices(this IServiceCollection services)
        {
            services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
            services.AddSingleton<IRuntimeSessionManager, RuntimeSessionManager>();
            services.AddSingleton<ISessionTimerService, SessionTimerService>();
            services.AddSingleton<IIdleDetectionService, IdleDetectionService>();
            services.AddSingleton<ISessionExpirationHandler, SessionExpirationHandler>();
            return services;
        }
    }
}
