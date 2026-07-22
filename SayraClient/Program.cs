using SayraClient;
using SayraClient.Commands;
using SayraClient.Services;
using SayraClient.Services.OfflineQueue;
using Sayra.Client.OfflineQueue.Extensions;
using Sayra.Client.Discovery.Services;
using Sayra.Client.GameLibrary;
using Sayra.Client.LocalAdmin;
using Sayra.Client.Launcher;
using Sayra.Client.Diagnostics.Extensions;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "client-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
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

// Register Power, Backup, and Sync Services
builder.Services.AddSingleton<IPowerManagementService, PowerManagementService>();
builder.Services.AddSingleton<IWorkstationBackupService, WorkstationBackupService>();
builder.Services.AddSingleton<IWorkstationSyncService, WorkstationSyncService>();

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

// Register Lifetime Orchestrator Hosted Service
builder.Services.AddHostedService<ClientAppLifetimeWorker>();

var host = builder.Build();
host.Run();
