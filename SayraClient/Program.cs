using SayraClient;
using SayraClient.Commands;
using SayraClient.Services;
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

// Register Application Services
builder.Services.AddSingleton<ProcessManager>();
builder.Services.AddSingleton<GameLauncher>();
builder.Services.AddSingleton<ProcessMonitor>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<KioskManager>();
builder.Services.AddSingleton<RecoveryManager>();
builder.Services.AddSingleton<SecurityManager>();
builder.Services.AddSingleton<SecureMessageValidator>();
builder.Services.AddSingleton<DiagnosticsService>();

// Register Security Services
builder.Services.AddSingleton<SessionKeyManager>();
builder.Services.AddSingleton<EncryptionManager>();
builder.Services.AddSingleton<IntegrityValidator>();
builder.Services.AddSingleton<AuthManager>();
builder.Services.AddSingleton<SecureTransportLayer>();

// Register Command System
builder.Services.AddSingleton<CommandParser>();
builder.Services.AddSingleton<CommandRouter>();
builder.Services.AddSingleton<ICommandHandler, SystemCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, AppCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, SessionCommandHandler>();

// MessageHandler depends on Command System
builder.Services.AddSingleton<MessageHandler>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<HeartbeatService>();
builder.Services.AddHostedService<WatchdogService>();
builder.Services.AddHostedService<AntiTamperService>();
builder.Services.AddHostedService<UpdateManager>();

var host = builder.Build();
host.Run();
