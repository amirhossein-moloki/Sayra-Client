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

// Register Command System
builder.Services.AddSingleton<CommandParser>();
builder.Services.AddSingleton<CommandRouter>();
builder.Services.AddSingleton<ICommandHandler, SystemCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, AppCommandHandler>();

// MessageHandler depends on Command System
builder.Services.AddSingleton<MessageHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
