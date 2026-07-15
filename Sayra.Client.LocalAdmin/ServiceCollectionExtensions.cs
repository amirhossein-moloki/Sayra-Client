using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.LocalAdmin.Authentication;
using Sayra.Client.LocalAdmin.Security;
using Sayra.Client.LocalAdmin.Services;
using Sayra.Client.LocalAdmin.Storage;

namespace Sayra.Client.LocalAdmin
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLocalAdmin(this IServiceCollection services, string? basePath = null)
        {
            services.AddSingleton<IPasswordHasher, PasswordHasher>();

            services.AddSingleton<ILocalAdminRepository>(sp =>
                new LocalAdminRepository(
                    basePath,
                    sp.GetService<ILogger<LocalAdminRepository>>()
                )
            );

            services.AddSingleton<IClientConfigurationRepository>(sp =>
                new ClientConfigurationRepository(
                    basePath,
                    sp.GetService<ILogger<ClientConfigurationRepository>>()
                )
            );

            services.AddSingleton<IAdminSessionManager, AdminSessionManager>();
            services.AddSingleton<ILocalAdminService, LocalAdminService>();
            services.AddSingleton<IClientConfigurationService, ClientConfigurationService>();

            services.AddHostedService<LocalAdminInitializer>();

            return services;
        }
    }
}
