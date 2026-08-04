using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Fleet.Policy.Infrastructure;
using Sayra.Client.Shared.Fleet.Policy.Interfaces;
using Sayra.Client.Shared.Fleet.Policy.Services;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.DependencyInjection
{
    /// <summary>
    /// Service collection extension methods to register Policy Administration Engine dependencies.
    /// </summary>
    public static class PolicyServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all Policy Administration Engine concrete services, repositories, validators, and coordinators.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddPolicyAdministration(this IServiceCollection services)
        {
            // Core Repository and Cache
            services.AddSingleton<IPolicyRepository, PolicyRepository>();
            services.AddSingleton<IPolicyCache, PolicyCache>();

            // Supporting Engines and Managers
            services.AddSingleton<IPolicyVersionManager, PolicyVersionManager>();
            services.AddSingleton<IPolicyAssignmentManager, PolicyAssignmentManager>();
            services.AddSingleton<IPolicyValidator, PolicyValidator>();
            services.AddSingleton<IPolicyDiffEngine, PolicyDiffEngine>();
            services.AddSingleton<IPolicyPreviewEngine, PolicyPreviewEngine>();
            services.AddSingleton<IComplianceEngine, ComplianceEngine>();
            services.AddSingleton<IRollbackManager, RollbackManager>();

            // Lifecycle Service (Coordinates CRUD/Cloning/Publishing)
            services.AddSingleton<IPolicyAdministrationEngine, PolicyLifecycleService>();

            // Top-Level Coordination Managers (Exposing interfaces)
            services.AddSingleton<IPolicyManager, PolicyManager>();

            // Register compatibility implementations for the existing Phase 9 interfaces
            services.AddSingleton<IPolicyAdministrationService>(sp => (PolicyLifecycleService)sp.GetRequiredService<IPolicyAdministrationEngine>());
            services.AddSingleton<IPolicyAssignmentService>(sp => (PolicyAssignmentManager)sp.GetRequiredService<IPolicyAssignmentManager>());
            services.AddSingleton<IPolicyComplianceService>(sp => (ComplianceEngine)sp.GetRequiredService<IComplianceEngine>());

            return services;
        }
    }
}
