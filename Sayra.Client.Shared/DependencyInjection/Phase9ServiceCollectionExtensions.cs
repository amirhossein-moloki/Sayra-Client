using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FluentValidation;
using Sayra.Client.Shared.Models.Phase9.Options;
using Sayra.Client.Shared.Models.Phase9.Dtos;
using Sayra.Client.Shared.Models.Phase9.Validation;
using Sayra.Client.Shared.Fleet.Security;
using Sayra.Client.Shared.Fleet.Services;
using Sayra.Client.Shared.Fleet.Queues;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Fleet.Assets;
using Sayra.Client.Shared.Fleet.Assets.Interfaces;
using Sayra.Client.Shared.Fleet.Assets.Collectors;
using Sayra.Client.Shared.Fleet.Assets.Services;
using Sayra.Client.Shared.Fleet.Maintenance;
using Sayra.Client.Shared.Fleet.Maintenance.Interfaces;
using Sayra.Client.Shared.Fleet.Maintenance.Services;

namespace Sayra.Client.Shared.DependencyInjection
{
    /// <summary>
    /// Service collection extension methods to register Phase 9 Foundation dependencies.
    /// </summary>
    public static class Phase9ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers options, validators, and core shared models for Phase 9.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddPhase9Foundation(this IServiceCollection services)
        {
            // Part 5: Options Registration & Bindings (bound to standard Microsoft.Extensions.Options pattern)
            services.AddOptions<FleetOptions>();
            services.AddOptions<MonitoringOptions>();
            services.AddOptions<DiagnosticsOptions>();
            services.AddOptions<TransferOptions>();
            services.AddOptions<MaintenanceOptions>();
            services.AddOptions<PolicyOptions>();
            services.AddOptions<AuditOptions>();
            services.AddOptions<BulkOperationOptions>();
            services.AddOptions<RemoteSupportOptions>();
            services.AddOptions<AdministrationOptions>();

            // Part 7 & 10: FluentValidation Structural DTO Validators
            services.AddTransient<IValidator<MachineQueryRequest>, MachineQueryRequestValidator>();
            services.AddTransient<IValidator<FleetQueryRequest>, FleetQueryRequestValidator>();

            // Phase 9 Stage 2: Fleet Management Engine
            services.AddFleetManagement();

            // Phase 9 Stage 3: Remote Command Framework
            services.AddRemoteCommandFramework();

            // Phase 9 Stage 4: Enterprise Live Monitoring Engine
            services.AddLiveMonitoring();

            // Phase 9 Stage 5: Enterprise Remote Diagnostics Engine
            services.AddRemoteDiagnostics();

            // Phase 9 Stage 6: Enterprise Remote File Management Engine
            services.AddRemoteFileManagement();

            // Phase 9 Stage 7: Enterprise Policy Administration Engine
            services.AddPolicyAdministration();

            // Phase 9 Stage 8: Enterprise Asset Management & Maintenance Engine
            services.AddAssetManagement();
            services.AddMaintenanceEngine();

            // Phase 9 Stage 9: Enterprise Bulk Operations Engine & Remote Assistance Framework
            services.AddBulkOperations();
            services.AddRemoteAssistance();

            services.AddTransient<IValidator<RemoteCommandRequest>, RemoteCommandRequestValidator>();
            services.AddTransient<IValidator<RemoteCommandResponse>, RemoteCommandResponseValidator>();
            services.AddTransient<IValidator<BulkOperationRequest>, BulkOperationRequestValidator>();
            services.AddTransient<IValidator<BulkOperationResponse>, BulkOperationResponseValidator>();
            services.AddTransient<IValidator<PolicyAssignmentRequest>, PolicyAssignmentRequestValidator>();
            services.AddTransient<IValidator<MaintenanceRequest>, MaintenanceRequestValidator>();
            services.AddTransient<IValidator<DiagnosticRequest>, DiagnosticRequestValidator>();
            services.AddTransient<IValidator<TransferRequest>, TransferRequestValidator>();
            services.AddTransient<IValidator<TransferResponse>, TransferResponseValidator>();
            services.AddTransient<IValidator<RemoteSupportRequest>, RemoteSupportRequestValidator>();
            services.AddTransient<IValidator<AuditQueryRequest>, AuditQueryRequestValidator>();
            services.AddTransient<IValidator<AdministrationReportRequest>, AdministrationReportRequestValidator>();

            return services;
        }

        /// <summary>
        /// Registers all Enterprise Remote File Management Engine dependencies.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddRemoteFileManagement(this IServiceCollection services)
        {
            services.AddSingleton<ISecurePathValidator, SecurePathValidator>();
            services.AddSingleton<IFileAuthorizationService, FileAuthorizationService>();
            services.AddSingleton<IChecksumService, ChecksumService>();
            services.AddSingleton<IBandwidthLimiter>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<TransferOptions>>();
                return new BandwidthLimiter(options.Value.ThrottleRateBytesPerSec);
            });
            services.AddSingleton<ITransferRepository, InMemoryTransferRepository>();
            services.AddSingleton<ITransferQueue, TransferQueue>();
            services.AddSingleton<ITransferManager, TransferManager>();
            services.AddSingleton<ITransferScheduler, TransferScheduler>();
            services.AddSingleton<IFileOperationCoordinator, FileOperationCoordinator>();
            services.AddSingleton<IRemoteFileService, RemoteFileManagementEngine>();

            return services;
        }

        /// <summary>
        /// Registers all Enterprise Asset Management dependencies.
        /// </summary>
        public static IServiceCollection AddAssetManagement(this IServiceCollection services)
        {
            // Repositories
            services.AddTransient<IAssetRepository, AssetRepository>();

            // Caches
            services.AddSingleton<IAssetCache, AssetCache>();
            services.AddSingleton<IInventoryCache, InventoryCache>();

            // Collectors
            services.AddTransient<IAssetCollector, HardwareInventoryCollector>();
            services.AddTransient<IAssetCollector, SoftwareInventoryCollector>();
            services.AddTransient<IAssetCollector, DriverInventoryCollector>();
            services.AddTransient<IAssetCollector, BIOSInventoryCollector>();
            services.AddTransient<IAssetCollector, FirmwareInventoryCollector>();
            services.AddTransient<IAssetCollector, StorageInventoryCollector>();
            services.AddTransient<IAssetCollector, NetworkInventoryCollector>();

            // Discovery Engine
            services.AddTransient<AssetDiscoveryEngine>();
            services.AddTransient<IInventoryCollector, AssetDiscoveryEngine>();

            // Management Service
            services.AddTransient<IAssetManagementService, AssetManagementService>();

            return services;
        }

        /// <summary>
        /// Registers all Enterprise Maintenance Engine dependencies.
        /// </summary>
        public static IServiceCollection AddMaintenanceEngine(this IServiceCollection services)
        {
            // Repositories
            services.AddTransient<IMaintenanceRepository, MaintenanceRepository>();

            // Cache
            services.AddSingleton<IMaintenanceCache, MaintenanceCache>();

            // Schedulers & State Managers
            services.AddTransient<IMaintenanceScheduler, MaintenanceScheduler>();
            services.AddTransient<IMaintenanceService, MaintenanceService>();

            return services;
        }
    }
}
