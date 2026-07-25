using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Application.Services;
using Sayra.Client.Shared.Security.GameProtection.Domain.Rules;
using Sayra.Client.Shared.Security.GameProtection.Infrastructure.Validators;

namespace Sayra.Client.Shared.Security.GameProtection.DependencyInjection;

public static class GameProtectionExtensions
{
    public static IServiceCollection AddGameProtectionServices(this IServiceCollection services, ProcessPolicy? defaultPolicy = null)
    {
        // 1. Register ProcessPolicy
        var policy = defaultPolicy ?? new ProcessPolicy();
        services.AddSingleton(policy);

        // 2. Register Game Protection Services with proper DI lifecycles
        services.AddSingleton<IIntegrityValidator, GameIntegrityValidator>();
        services.AddSingleton<IProcessPolicyEvaluator, ProcessPolicyEvaluator>();
        services.AddSingleton<IThreatReporter, ThreatReporter>();
        services.AddSingleton<IProcessSecurityMonitor, ProcessSecurityMonitor>();
        services.AddSingleton<ConfigFileTamperWatcher>();

        return services;
    }
}
