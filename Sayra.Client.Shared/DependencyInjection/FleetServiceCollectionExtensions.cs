using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sayra.Client.Shared.Fleet.Infrastructure;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Fleet.Services;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering Fleet Management services in the Dependency Injection container.
    /// </summary>
    public static class FleetServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all repositories, managers, search engines, query services, and caches for Fleet Management.
        /// </summary>
        public static IServiceCollection AddFleetManagement(this IServiceCollection services)
        {
            // Database Context
            services.TryAddSingleton<IFleetDatabaseContext, FleetDatabaseContext>();

            // Repositories
            services.TryAddTransient<IMachineRepository, MachineRepository>();
            services.TryAddTransient<IGroupRepository, GroupRepository>();
            services.TryAddTransient<IRegionRepository, RegionRepository>();
            services.TryAddTransient<IDepartmentRepository, DepartmentRepository>();
            services.TryAddTransient<ITagRepository, TagRepository>();
            services.TryAddTransient<ISnapshotRepository, SnapshotRepository>();
            services.TryAddTransient<IHealthRepository, HealthRepository>();
            services.TryAddTransient<IInventoryRepository, InventoryRepository>();

            // Enterprise Cache (must be Singleton to preserve in-memory state)
            services.TryAddSingleton<IFleetCache, FleetCache>();

            // Core Engines & Business Services
            services.TryAddTransient<ITagManager, TagManager>();
            services.TryAddTransient<IOrganizationService, OrganizationService>();
            services.TryAddTransient<IFleetSearchEngine, FleetSearchEngine>();
            services.TryAddTransient<IFleetQueryService, FleetQueryService>();
            services.TryAddTransient<IFleetSynchronizationService, FleetSynchronizationService>();

            // Phase 9 IFleetManager Coordinator (Transient/Scoped as it delegates to repos/caches)
            services.TryAddTransient<IFleetManager, FleetManager>();

            return services;
        }
    }
}
