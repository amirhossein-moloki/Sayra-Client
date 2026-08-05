using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Fleet.BulkOperations;
using Sayra.Client.Shared.Interfaces.Fleet;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.DependencyInjection
{
    /// <summary>
    /// Service collection extensions for registering Stage 9 Bulk Operations Engine dependencies.
    /// </summary>
    public static class BulkOperationsServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all core bulk operation services, managers, Schedulers, Senders, and repositories.
        /// </summary>
        public static IServiceCollection AddBulkOperations(this IServiceCollection services)
        {
            // Repository
            services.AddSingleton<IBulkOperationRepository, BulkOperationRepository>();

            // Targeting
            services.AddSingleton<ITargetResolver, TargetResolver>();

            // Execution Manager & Pipeline
            services.AddSingleton<BulkExecutionManager>();

            // Retry and Recovery Manager
            services.AddSingleton<BulkRetryManager>();

            // Rollback coordinator
            services.AddSingleton<BulkRollbackManager>();

            // Coordinator & Engine Schedulers
            services.AddSingleton<IBulkOperationCoordinator, BulkOperationCoordinator>();
            services.AddSingleton<Sayra.Client.Shared.Interfaces.Phase9.IBulkOperationService, BulkOperationEngine>();

            return services;
        }
    }
}
