using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Application.Services;

namespace Sayra.Client.Shared.Runtime.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRuntimeServices(this IServiceCollection services)
        {
            services.AddSingleton<IRuntimeEventPublisher, RuntimeEventPublisher>();
            services.AddSingleton<IRuntimeStateManager, RuntimeStateManager>();
            services.AddSingleton<IRuntimeSessionManager, RuntimeSessionManager>();
            services.AddSingleton<IRuntimeContextProvider, RuntimeContextProvider>();
            return services;
        }
    }
}
