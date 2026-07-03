using SayraClient;
using SayraClient.Commands;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Sayra Client";
});

// Register Core Services
builder.Services.AddSingleton<ReconnectManager>();
builder.Services.AddSingleton<NetworkManager>();

// Register Command System
builder.Services.AddSingleton<CommandParser>();
builder.Services.AddSingleton<CommandRouter>();
builder.Services.AddSingleton<ICommandHandler, SystemCommandHandler>();

// MessageHandler depends on Command System
builder.Services.AddSingleton<MessageHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
