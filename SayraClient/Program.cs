using SayraClient;
using SayraClient.Commands;
using SayraClient.Services;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Sayra Client";
});

// Register Core Services
builder.Services.AddSingleton<ReconnectManager>();
builder.Services.AddSingleton<NetworkManager>();

// Register Application Services
builder.Services.AddSingleton<ProcessManager>();
builder.Services.AddSingleton<GameLauncher>();
builder.Services.AddSingleton<ProcessMonitor>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<KioskManager>();
builder.Services.AddSingleton<RecoveryManager>();

// Register Command System
builder.Services.AddSingleton<CommandParser>();
builder.Services.AddSingleton<CommandRouter>();
builder.Services.AddSingleton<ICommandHandler, SystemCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, AppCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, SessionCommandHandler>();

// MessageHandler depends on Command System
builder.Services.AddSingleton<MessageHandler>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<WatchdogService>();

var host = builder.Build();
host.Run();
