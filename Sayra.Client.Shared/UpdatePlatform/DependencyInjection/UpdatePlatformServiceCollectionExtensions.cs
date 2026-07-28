using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Sayra.Client.Shared.UpdatePlatform.Application.Validation;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;
using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

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

            services.AddOptions<TelemetryOptions>()
                .Validate(o => o.QueueLimit > 0, "QueueLimit must be greater than zero.")
                .Validate(o => o.ReportingIntervalSeconds > 0, "ReportingIntervalSeconds must be greater than zero.");

            services.AddOptions<MonitoringOptions>()
                .Validate(o => o.MinStorageBytes >= 0, "MinStorageBytes cannot be negative.")
                .Validate(o => o.CheckIntervalMinutes > 0, "CheckIntervalMinutes must be greater than zero.");

            services.AddOptions<ReportingOptions>()
                .Validate(o => o.MaxRetryAttempts >= 0, "MaxRetryAttempts cannot be negative.")
                .Validate(o => o.BaseDelaySeconds > 0, "BaseDelaySeconds must be greater than zero.");

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

            // Register standard HTTP client factory support
            services.AddHttpClient();

            // Phase 6 Part 8 Windows Integration & Enterprise Security Implementations
            services.AddSingleton<IAuthenticodeVerifier, AuthenticodeVerifier>();
            services.AddSingleton<ICertificatePinningService, CertificatePinningService>();
            services.AddSingleton<IWindowsEventLogger, WindowsEventLogger>();

            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IWindowsServiceManager, WindowsServiceManager>();
                services.AddSingleton<IPrivilegeManager, PrivilegeManager>();
            }
            else
            {
                services.AddSingleton<IWindowsServiceManager, MockWindowsServiceManager>();
                services.AddSingleton<IPrivilegeManager, MockPrivilegeManager>();
            }

            services.AddSingleton<IFileSecurityValidator, FileSecurityValidator>();

            // Phase 6 Part 9 Telemetry, Monitoring & Administrative Integration Implementations
            services.AddHttpClient("AdminIntegrationClient")
                .ConfigurePrimaryHttpMessageHandler(sp =>
                {
                    var pinningService = sp.GetRequiredService<ICertificatePinningService>();
                    var logger = sp.GetRequiredService<ILogger<AdminIntegrationClient>>();
                    var reportingOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReportingOptions>>().Value;

                    var handler = new SocketsHttpHandler();
                    handler.SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                    {
                        EnabledSslProtocols = SslProtocols.Tls13, // Force TLS 1.3
                        RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                        {
                            if (cert == null)
                            {
                                logger.LogError("Certificate validation failed: No certificate presented by server.");
                                return false;
                            }

                            var cert2 = cert as X509Certificate2 ?? new X509Certificate2(cert);

                            // Strict Defense-in-Depth: Validate hostname mismatch and other basic policy errors first
                            if (errors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch))
                            {
                                logger.LogError("Certificate validation failed: Remote hostname mismatch.");
                                return false;
                            }

                            if (reportingOptions.EnforceCertificatePinning)
                            {
                                string[] expectedThumbprints = !string.IsNullOrEmpty(reportingOptions.PinnedCertificateThumbprint)
                                    ? new[] { reportingOptions.PinnedCertificateThumbprint }
                                    : Array.Empty<string>();

                                string[] expectedPublicKeyHashes = !string.IsNullOrEmpty(reportingOptions.PinnedPublicKeyHash)
                                    ? new[] { reportingOptions.PinnedPublicKeyHash }
                                    : Array.Empty<string>();

                                var validationResult = pinningService.ValidateCertificate(
                                    cert2,
                                    expectedThumbprints,
                                    expectedPublicKeyHashes,
                                    Array.Empty<string>());

                                if (!validationResult.Success)
                                {
                                    logger.LogError("Certificate pinning validation failed: {Message}", validationResult.ErrorMessage);
                                    return false;
                                }
                            }
                            else
                            {
                                // If pinning is not enforced, standard validation checks apply
                                if (errors != System.Net.Security.SslPolicyErrors.None)
                                {
                                    logger.LogError("Certificate standard validation failed: {Errors}", errors);
                                    return false;
                                }
                            }

                            logger.LogInformation("Certificate validation succeeded.");
                            return true;
                        }
                    };
                    return handler;
                });

            services.AddSingleton<IAdminIntegrationClient, AdminIntegrationClient>();
            services.AddSingleton<AdminIntegrationClient>(sp => (AdminIntegrationClient)sp.GetRequiredService<IAdminIntegrationClient>());
            services.AddSingleton<ITelemetryOfflineQueue, TelemetryOfflineQueue>();
            services.AddSingleton<ITelemetryReporter, TelemetryReporter>();
            services.AddSingleton<IHealthMonitor, HealthMonitor>();
            services.AddSingleton<IDiagnosticReporter, DiagnosticReporter>();

            return services;
        }
    }
}
