using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Sayra.Client.Shared.UpdatePlatform.Application.Validation;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;
using System;

namespace Sayra.Client.Shared.UpdatePlatform.DependencyInjection
{
    /// <summary>
    /// Service collection extension methods to register Update Platform Foundation dependencies.
    /// </summary>
    public static class UpdatePlatformServiceCollectionExtensions
    {
        /// <summary>
        /// Registers options, validators, and core shared interfaces for the Update Platform.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddUpdatePlatformFoundation(this IServiceCollection services)
        {
            // Register and validate Options
            services.AddOptions<UpdateOptions>();
            services.AddOptions<RollbackOptions>();
            services.AddOptions<DownloadOptions>();
            services.AddOptions<StorageOptions>();

            services.AddOptions<SchedulerOptions>()
                .Validate(o => o.CheckIntervalMinutes > 0, "CheckIntervalMinutes must be greater than zero.")
                .Validate(o => o.JitterSeconds >= 0, "JitterSeconds cannot be negative.");

            services.AddOptions<DeploymentOptions>();

            services.AddOptions<MaintenanceWindowOptions>()
                .Validate(o => TimeSpan.TryParse(o.StartTimeUtc, out _), "StartTimeUtc must be a valid parsable TimeSpan string.")
                .Validate(o => TimeSpan.TryParse(o.EndTimeUtc, out _), "EndTimeUtc must be a valid parsable TimeSpan string.")
                .Validate(o => o.MaxOccupancyPercentage >= 0 && o.MaxOccupancyPercentage <= 100, "MaxOccupancyPercentage must be between 0 and 100.");

            services.AddOptions<RolloutOptions>()
                .Validate(o => o.RolloutPercentage >= 0 && o.RolloutPercentage <= 100, "RolloutPercentage must be between 0 and 100.");

            // Register Validators
            services.AddTransient<IVersionValidator, VersionValidator>();
            services.AddTransient<IDependencyValidator, DependencyValidator>();
            services.AddTransient<IManifestValidator, ManifestValidator>();
            services.AddTransient<IUpdateValidator, UpdateValidator>();

            // Phase 6 Part 2 Core Service Implementations
            services.AddSingleton<IManifestParser, ManifestParser>();
            services.AddSingleton<ISignatureVerifier, SignatureVerifier>();
            services.AddTransient<IPackageValidator, PackageValidator>();
            services.AddTransient<IPackageReader, PackageReader>();
            services.AddTransient<IPackageVerifier, PackageVerifier>();

            // Phase 6 Part 3 Download Engine Core Implementations
            services.AddSingleton<IBandwidthLimiter, BandwidthLimiter>();
            services.AddSingleton<IMirrorSelector, MirrorSelector>();
            services.AddSingleton<IDownloadStateStore, DownloadStateStore>();
            services.AddTransient<IProgressReporter, ProgressReporter>();
            services.AddTransient<IChunkDownloader, ChunkDownloader>();
            services.AddSingleton<IDownloadManager, DownloadManager>();

            // Phase 6 Part 4 Scheduling & Deployment Policy Implementations
            services.AddSingleton<IMaintenanceWindowService, MaintenanceWindowService>();
            services.AddSingleton<IDeploymentPolicyEvaluator, DeploymentPolicyEvaluator>();
            services.AddSingleton<IRolloutService, RolloutService>();
            services.AddTransient<IEligibilityEvaluator, EligibilityEvaluator>();
            services.AddSingleton<IUpdateScheduler, UpdateScheduler>();

            // Phase 6 Part 5 Installation Engine Implementations
            services.AddSingleton<IRestartManagerService, WindowsRestartManager>();
            services.AddSingleton<IAtomicFileReplacer, AtomicFileReplacer>();
            services.AddTransient<IInstallationValidator, InstallationValidator>();
            services.AddTransient<IInstallationStateMachine, InstallationStateMachine>();
            services.AddSingleton<Func<IInstallationStateMachine>>(sp => () => sp.GetRequiredService<IInstallationStateMachine>());
            services.AddSingleton<IInstallationCoordinator, InstallationCoordinator>();
            services.AddSingleton<IInstallerEngine, InstallerEngine>();
            services.AddSingleton<IUpdateManager, Sayra.Client.Shared.UpdatePlatform.Application.Services.UpdateManager>();

            // Phase 6 Part 6 Rollback & Recovery Platform Implementations
            services.AddSingleton<IBackupManager, BackupManager>();
            services.AddSingleton<ISnapshotManager, SnapshotManager>();
            services.AddTransient<IRecoveryValidator, RecoveryValidator>();
            services.AddSingleton<IRecoveryStateMachine, RecoveryStateMachine>();
            services.AddSingleton<Func<IRecoveryStateMachine>>(sp => () => sp.GetRequiredService<IRecoveryStateMachine>());
            services.AddSingleton<IRollbackEngine, RollbackEngine>();
            services.AddSingleton<IRecoveryEngine, RecoveryEngine>();

            // Phase 6 Part 7 Update Storage, Cache & History Implementations
            services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();
            services.AddSingleton<IDatabaseHealthMonitor, DatabaseHealthMonitor>();
            services.AddSingleton<IDatabaseRecoveryService, DatabaseRecoveryService>();
            services.AddSingleton<IUpdateHistoryRepository, UpdateHistoryRepository>();
            services.AddSingleton<IRollbackHistoryRepository, RollbackHistoryRepository>();
            services.AddSingleton<ICacheManager, CacheManager>();
            services.AddSingleton<IStorageQuotaManager, StorageQuotaManager>();
            services.AddSingleton<IUpdateRepository, UpdateRepository>();

            // Phase 6 Part 8 Windows Integration & Enterprise Security Implementations
            services.AddSingleton<IAuthenticodeVerifier, AuthenticodeVerifier>();
            services.AddSingleton<ICertificatePinningService, CertificatePinningService>();
            services.AddSingleton<IWindowsEventLogger, WindowsEventLogger>();
            services.AddSingleton<IWindowsServiceManager, WindowsServiceManager>();
            services.AddSingleton<IPrivilegeManager, PrivilegeManager>();
            services.AddSingleton<IFileSecurityValidator, FileSecurityValidator>();

            return services;
        }
    }
}
