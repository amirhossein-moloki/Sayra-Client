using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Launch.Application.Services;
using Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Process;
using Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Sessions;
using Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Tokens;
using Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Sandbox;
using Sayra.Client.Shared.Runtime.Launch.Infrastructure.Windows.Registry;

namespace Sayra.Client.Shared.Runtime.Launch.DependencyInjection
{
    public static class SecureLaunchExtensions
    {
        public static IServiceCollection AddSecureLaunchServices(this IServiceCollection services)
        {
            services.AddSingleton<IUserSessionProvider, UserSessionProvider>();
            services.AddSingleton<IUserTokenService, UserTokenService>();
            services.AddSingleton<IProcessCreator, ProcessCreator>();
            services.AddSingleton<ILaunchProfileProvider, LaunchProfileProvider>();
            services.AddSingleton<ILaunchValidator, LaunchValidator>();
            services.AddSingleton<ISandboxManager, WindowsSandboxManager>();
            services.AddSingleton<IRegistryVirtualizationManager, WindowsRegistryVirtualizationManager>();
            services.AddSingleton<ISecureLauncher, SecureLauncher>();

            return services;
        }
    }
}
