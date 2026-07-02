using SayraClient;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Sayra Client";
});

builder.Services.AddSingleton<ReconnectManager>();
builder.Services.AddSingleton<MessageHandler>();
builder.Services.AddSingleton<NetworkManager>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
