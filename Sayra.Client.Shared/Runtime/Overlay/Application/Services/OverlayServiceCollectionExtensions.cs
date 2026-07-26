using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.Overlay.Application.Services
{
    /// <summary>
    /// Service collection extension class for cleanly registering Overlay services.
    /// </summary>
    public static class OverlayServiceCollectionExtensions
    {
        public static IServiceCollection AddOverlayServices(this IServiceCollection services)
        {
            services.AddSingleton<IOverlayDataProvider, OverlayDataProvider>();
            services.AddSingleton<IOverlayManager, OverlayManager>();
            return services;
        }
    }
}
