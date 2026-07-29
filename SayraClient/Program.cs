using SayraClient;
using SayraClient.Commands;
using SayraClient.Services.Recovery;
using SayraClient.Services.Recovery.Strategies;
using SayraClient.Services.Recovery.Exporters;
using SayraClient.Services;
using SayraClient.Security.Transport;
using SayraClient.Services.OfflineQueue;
using Sayra.Client.OfflineQueue;
using Sayra.Client.Configuration.Conflict;
using Sayra.Client.Configuration.Rollback;
using Sayra.Client.Configuration.Storage;
using Sayra.Client.Configuration.Synchronization;
using Sayra.Client.Configuration.Validation;
using Sayra.Client.Configuration.Versioning;
using SayraClient.Services.Configuration;
using SayraClient.Services.Windows;
using Sayra.Client.OfflineQueue.Extensions;
using SayraClient.Security.Integrity;
using Sayra.Client.Discovery.Services;
using Sayra.Client.GameLibrary;
using Sayra.Client.LocalAdmin;
using Sayra.Client.Launcher;
using Sayra.Client.Diagnostics.Extensions;
using Sayra.Client.Diagnostics.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Services;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Runtime.Infrastructure.DependencyInjection;
using Sayra.Client.Shared.Security.GameProtection.DependencyInjection;
using Sayra.Client.Shared.Runtime.Launch.DependencyInjection;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.DependencyInjection;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using SayraClient.Kiosk.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog with structured JSON rotation pipelines, restricting storage to 10MB x 5 files
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(new Serilog.Formatting.Json.JsonFormatter(),
        Path.Combine(AppContext.BaseDirectory, "logs", "client.log"),
        fileSizeLimitBytes: 10 * 1024 * 1024,
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 5)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Services.AddSerilog();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Sayra Client";
});

// Register Runtime Services
builder.Services.AddRuntimeServices();
builder.Services.AddSecureLaunchServices();
builder.Services.AddProcessSupervisorServices();

// Register Core Services
builder.Services.AddSingleton<ReconnectManager>();
builder.Services.AddSingleton<TransportPolicy>();
builder.Services.AddSingleton<TlsConnectionManager>();
builder.Services.AddSingleton<TcpClientManager>();
builder.Services.AddSingleton<ClientStateManager>();

// Register Game Library Component
builder.Services.AddGameLibrary();

// Register Local Admin Component
builder.Services.AddLocalAdmin();

// Register Launcher Component
builder.Services.AddLauncherServices();

// Register Diagnostics Component
builder.Services.AddDiagnosticsServices(builder.Configuration);

// Register Application Services
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<IKioskSecurityService, KioskSecurityService>();
builder.Services.AddKioskSecurityServices();
builder.Services.AddSingleton<RecoveryManager>();
builder.Services.AddSingleton<SecurityManager>();
builder.Services.AddSingleton<SecureMessageValidator>();
builder.Services.AddSingleton<DiagnosticsService>();

// Register Discovery Service
builder.Services.AddSingleton<UdpDiscoveryClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    int port = int.Parse(config["ServerDiscovery:UdpPort"] ?? "37020");
    return new UdpDiscoveryClient(sp.GetRequiredService<ILogger<UdpDiscoveryClient>>(), port);
});
builder.Services.AddSingleton<DiscoveryValidator>(sp =>
{
    return new DiscoveryValidator(
        sp.GetRequiredService<ILogger<DiscoveryValidator>>(),
        Path.Combine(AppContext.BaseDirectory, "server_public.key"));
});
builder.Services.AddSingleton<IDiscoveryService, DiscoveryManager>();

// Register Security Services
builder.Services.AddSingleton<SessionKeyManager>();
builder.Services.AddSingleton<HashRegistry>();
builder.Services.AddSingleton<ICryptographyService, CryptographyService>();
builder.Services.AddSingleton<IIntegrityValidator, IntegrityValidator>();
builder.Services.AddSingleton<ISecureIpcPolicyManager, SecureIpcPolicyManager>();
builder.Services.AddSingleton<AuthManager>();
builder.Services.AddSingleton<SecureTransportLayer>();
builder.Services.AddSingleton<RuntimeIntegrityMonitor>();
builder.Services.AddGameProtectionServices(); // Register Game Protection and Runtime Monitoring Subsystem (Track 4.6)

// Register Configuration Sync Engine Components
builder.Services.AddSingleton<ConfigurationValidator>();
builder.Services.AddSingleton<ConfigurationSignatureValidator>();
builder.Services.AddSingleton<ConfigurationVersionManager>();
builder.Services.AddSingleton<ConfigurationDeltaEngine>();
builder.Services.AddSingleton<ConfigurationConflictResolver>();
builder.Services.AddSingleton<ConfigurationRollbackManager>();
builder.Services.AddSingleton<ConfigurationApplyService>();
builder.Services.AddSingleton<IConfigurationApiClient, MockConfigurationApiClient>();
builder.Services.AddSingleton<IConfigurationSynchronizationService, ConfigurationSynchronizationService>();

// Register Power, Backup, and Sync Services
builder.Services.AddSingleton<IPowerManagementService, PowerManagementService>();
builder.Services.AddSingleton<IWorkstationBackupService, WorkstationBackupService>();
builder.Services.AddSingleton<IWorkstationSyncService, WorkstationSyncService>();

// Register Windows Native Enterprise Services
builder.Services.AddSingleton<IWindowsEventLogService, WindowsEventLogService>();
builder.Services.AddSingleton<IRestartManagerHelper, RestartManagerHelper>();
builder.Services.AddSingleton<RegistryWatcher>();
builder.Services.AddSingleton<FileSystemTamperWatcher>();
builder.Services.AddSingleton<WtsSessionChangeMonitor>();
builder.Services.AddSingleton<EtwProcessMonitor>();
builder.Services.AddSingleton<PowerStatusChangeHandler>();
builder.Services.AddSingleton<TaskSchedulerFallbackService>();

// Register Offline Queue Services
builder.Services.AddOfflineQueue();

// Register Update Services
builder.Services.AddSingleton<UpdateVerificationService>();
builder.Services.AddSingleton<BackupService>();

// Register Command System
builder.Services.AddSingleton<CommandParser>();
builder.Services.AddSingleton<CommandRouter>();
builder.Services.AddSingleton<ICommandHandler, SystemCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, AppCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, SessionCommandHandler>();

// Register Remote Command Foundation & Security Stage 1 Services
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Security.ICryptoService, SayraClient.RemoteOperations.Security.CryptoService>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Security.ISignatureVerifier, SayraClient.RemoteOperations.Security.SignatureVerifier>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Security.IMessageAuthenticator, SayraClient.RemoteOperations.Security.MessageAuthenticator>();

builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.ICommandResultReporter, SayraClient.RemoteOperations.Services.CommandResultReporter>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandDispatcher, SayraClient.RemoteOperations.Services.RemoteCommandDispatcher>();

// Register Stage 2 Secure Local Database, Repository, Queue, Retry, and Audit Services
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IDatabaseMigrationService, SayraClient.RemoteOperations.Services.DatabaseMigrationService>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.ILocalDatabaseService, SayraClient.RemoteOperations.Services.LocalDatabaseService>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandRepository, SayraClient.RemoteOperations.Services.RemoteCommandRepository>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IDeadLetterQueue, SayraClient.RemoteOperations.Services.DeadLetterQueue>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IOfflineCommandQueue, SayraClient.RemoteOperations.Services.OfflineCommandQueue>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IAuditService, SayraClient.RemoteOperations.Services.AuditService>();
builder.Services.AddSingleton<SayraClient.RemoteOperations.Services.CommandRetryWorker>();

builder.Services.AddSingleton<SayraClient.RemoteOperations.Services.RemoteCommandEngine>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandEngine>(sp => sp.GetRequiredService<SayraClient.RemoteOperations.Services.RemoteCommandEngine>());

// Register Remote Command Handlers
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.LockPcCommandHandler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.UnlockPcCommandHandler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.ShutdownCommandHandler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.RestartCommandHandler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.LaunchGameCommandHandler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.CloseGameCommandHandler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.KillProcessCommandHandler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.WakeOnLanCommandHandler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.MaintenanceModeCommandHandler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.RestartApplicationCommandHandler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IRemoteCommandHandler, SayraClient.RemoteOperations.Handlers.RestartServiceCommandHandler>();

// Register Policy Engine & System Controllers
builder.Services.AddSingleton<SayraClient.RemoteOperations.Services.RegistryPolicyManager>();
builder.Services.AddSingleton<SayraClient.RemoteOperations.Services.UsbPolicyManager>();
builder.Services.AddSingleton<SayraClient.RemoteOperations.Services.NetworkPolicyManager>();
builder.Services.AddSingleton<SayraClient.RemoteOperations.Services.SessionPolicyManager>();
builder.Services.AddSingleton<SayraClient.RemoteOperations.Services.PolicyValidator>();
builder.Services.AddSingleton<SayraClient.RemoteOperations.Services.PolicyRollbackService>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IPolicyRepository, SayraClient.RemoteOperations.Services.PolicyRepository>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IPolicyEngine, SayraClient.RemoteOperations.Services.PolicyEngine>();
builder.Services.AddSingleton<SayraClient.RemoteOperations.Services.PolicySynchronizationService>();

// Register Stage 5 Fleet Management & Operations Services
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IGroupRepository, SayraClient.RemoteOperations.Services.GroupRepository>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IFleetManager, SayraClient.RemoteOperations.Services.FleetManager>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IBulkOperationService, SayraClient.RemoteOperations.Services.BulkOperationService>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IAlertManager, SayraClient.RemoteOperations.Services.AlertEngine>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IEnterpriseOperationService, SayraClient.RemoteOperations.Services.EnterpriseOperationService>();
builder.Services.AddSingleton<SayraClient.RemoteOperations.Services.OperationCoordinator>();

// Register Stage 6 Enterprise Advertisement Platform Services
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IAdvertisementRepository, SayraClient.RemoteOperations.Services.AdvertisementRepository>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IAdDownloadManager, SayraClient.RemoteOperations.Services.AdDownloadManager>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IAdvertisementCache, SayraClient.RemoteOperations.Services.AdvertisementCache>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.ICampaignScheduler, SayraClient.RemoteOperations.Services.CampaignScheduler>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IAdvertisementEngine, SayraClient.RemoteOperations.Services.AdvertisementEngine>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IMediaPlaybackService, SayraClient.RemoteOperations.Services.MediaPlaybackService>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.IImpressionTracker, SayraClient.RemoteOperations.Services.ImpressionTracker>();

// Register MessageHandler (depends on Command System)
builder.Services.AddSingleton<MessageHandler>();

// ==========================================
// REGISTER SPRINT 1 FOUNDATION INFRASTRUCTURE
// ==========================================
builder.Services.Configure<Sayra.Client.Shared.Models.Recovery.HealthMonitorOptions>(builder.Configuration.GetSection("HealthMonitor"));
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Recovery.IHealthMonitor, SayraClient.Services.Recovery.HealthMonitor>();

// Register Stage 3 Self-Healing Engine Services
builder.Services.AddSingleton<RecoveryQueue>();
builder.Services.AddSingleton<LoopDetector>();
builder.Services.AddSingleton<RecoveryDependencyResolver>();
builder.Services.AddSingleton<RecoveryMetricsCollector>();
builder.Services.AddSingleton<BackoffDelayCalculator>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Recovery.ISelfHealingService, SayraClient.Services.Recovery.SelfHealingService>();

// Register Stage 3 Pluggable Recovery Strategies
builder.Services.AddSingleton<IRecoveryActionStrategy, DatabaseRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, NetworkRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, TcpRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, IpcRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, ConfigurationReloadRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, QueueWorkersRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, BackgroundWorkersRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, PluginHostRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, OverlayRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, DownloadManagerRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, SynchronizationRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, NotificationQueueRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, RemoteCommandsRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, TelemetryRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, LoggingRecoveryStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, RestartWorkerStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, EscalateToAdminStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, RebootWorkstationStrategy>();
builder.Services.AddSingleton<IRecoveryActionStrategy, ShutdownWorkstationStrategy>();

// ==========================================
// REGISTER RESILIENCE & DIAGNOSTICS PLATFORM
// ==========================================
builder.Services.Configure<Sayra.Client.Shared.Models.Recovery.ResourceMonitorOptions>(builder.Configuration.GetSection("Recovery:ResourceMonitor"));
builder.Services.Configure<Sayra.Client.Shared.Models.Recovery.RecoveryDiagnosticsOptions>(builder.Configuration.GetSection("Recovery:Diagnostics"));

// Exporters
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Recovery.IDiagnosticsExporter, PlainTextDiagnosticsExporter>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Recovery.IDiagnosticsExporter, JsonDiagnosticsExporter>();

// Engines
builder.Services.AddSingleton<ResourceMonitor>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Recovery.IResourceMonitor>(sp => sp.GetRequiredService<ResourceMonitor>());

builder.Services.AddSingleton<SecurityHardeningService>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Recovery.ISecurityHardeningService>(sp => sp.GetRequiredService<SecurityHardeningService>());

builder.Services.AddSingleton<CrashRecoveryManager>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Recovery.ICrashRecoveryManager>(sp => sp.GetRequiredService<CrashRecoveryManager>());

builder.Services.AddSingleton<GracefulShutdownService>();
builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Recovery.IGracefulShutdownService>(sp => sp.GetRequiredService<GracefulShutdownService>());

builder.Services.AddSingleton<Sayra.Client.Shared.Interfaces.Recovery.IRecoveryDiagnosticsEngine, RecoveryDiagnosticsEngine>();

builder.Services.AddSingleton<IServiceHealthMonitor, ServiceHealthMonitor>();
builder.Services.AddSingleton<IWorkerSupervisor, WorkerSupervisor>();
builder.Services.AddSingleton<IHeartbeatManager, HeartbeatManager>();
builder.Services.AddSingleton<IModuleLifecycleManager, ModuleLifecycleManager>();
builder.Services.AddSingleton<IStartupPipeline, StartupPipeline>();
builder.Services.AddSingleton<IShutdownCoordinator, ShutdownCoordinator>();
builder.Services.AddSingleton<IDependencyValidator, DependencyValidator>();

// Register Logging and Audit Context Providers and Services
builder.Services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
builder.Services.AddSingleton<IEventDispatcher, EventDispatcher>();
builder.Services.AddSingleton<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddSingleton<IAuditLogger, AuditLogger>();
builder.Services.AddSingleton<LogBatchingManager>();

// Register All Supervised Workers and Modules as Singletons
builder.Services.AddSingleton<IpcServer>();
builder.Services.AddSingleton<Worker>();
builder.Services.AddSingleton<HeartbeatService>();
builder.Services.AddSingleton<WatchdogService>();
builder.Services.AddSingleton<AntiTamperService>();
builder.Services.AddSingleton<WhitelistingService>();
builder.Services.AddSingleton<UpdateManager>();
builder.Services.AddSingleton<LauncherIntegrationService>();
builder.Services.AddSingleton<QueueProcessorWorker>();
builder.Services.AddSingleton<QueueHealthWorker>();
builder.Services.AddSingleton<EventQueueBatchingWorker>();
builder.Services.AddSingleton<LogCompressionWorker>();
builder.Services.AddSingleton<ConfigurationSyncScheduler>();

// Register Lifetime Orchestrator Hosted Service
builder.Services.AddHostedService<ClientAppLifetimeWorker>();

var host = builder.Build();
host.Run();
