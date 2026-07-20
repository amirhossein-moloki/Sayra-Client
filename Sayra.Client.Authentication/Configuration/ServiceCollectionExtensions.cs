using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Models;
using Sayra.Client.Authentication.Providers;
using Sayra.Client.Authentication.Services;

namespace Sayra.Client.Authentication.Configuration
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSayraAuthentication(this IServiceCollection services)
        {
            // Register Context (shared singleton)
            var userContext = new UserContext();
            services.AddSingleton<UserContext>(userContext);
            services.AddSingleton<IUserContext>(sp => sp.GetRequiredService<UserContext>());

            // Register Providers
            services.AddTransient<IAuthenticationProvider, LocalAdminAuthenticationProvider>();
            services.AddTransient<IAuthenticationProvider, ReservationAuthenticationProvider>();
            services.AddTransient<IAuthenticationProvider, CachedAuthenticationProvider>();
            services.AddTransient<IAuthenticationProvider, OfflineAuthenticationProvider>();
            services.AddTransient<IAuthenticationProvider, ServerAuthenticationProvider>();

            // Register Services
            services.AddSingleton<IServerReservationService, ServerReservationService>();
            services.AddSingleton<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<IAuthorizationService, AuthorizationService>();

            return services;
        }
    }
}
