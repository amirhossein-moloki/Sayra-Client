using SayraClient;
using SayraClient.Commands;
using SayraClient.Services;
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
using Sayra.Client.Discovery.Services;
using Sayra.Client.GameLibrary;
using Sayra.Client.LocalAdmin;
using Sayra.Client.Launcher;
using Sayra.Client.Diagnostics.Extensions;
using Sayra.Client.Diagnostics.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Services;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

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

// Register Core Services
builder.Services.AddSingleton<ReconnectManager>();
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
builder.Services.AddSingleton<KioskManager>();
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
builder.Services.AddSingleton<EncryptionManager>();
builder.Services.AddSingleton<IntegrityValidator>();
builder.Services.AddSingleton<AuthManager>();
builder.Services.AddSingleton<SecureTransportLayer>();

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

// Register MessageHandler (depends on Command System)
builder.Services.AddSingleton<MessageHandler>();

// ==========================================
// REGISTER SPRINT 1 FOUNDATION INFRASTRUCTURE
// ==========================================
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
