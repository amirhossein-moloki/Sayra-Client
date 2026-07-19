using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using Sayra.Client.LocalAdmin;
using Sayra.Client.LocalAdmin.Services;
using Sayra.Client.LocalAdmin.Storage;
using Sayra.Client.LocalAdmin.Authentication;
using Sayra.Client.LocalAdmin.Security;
using SayraClient.Services;
using SayraClient;
using SayraClient.Commands;
using System.IO;
using System.Threading.Tasks;

namespace Sayra.UI
{
    public partial class App : Application
    {
        public static bool IsAdminLoggedIn { get; set; }
        public static IServiceProvider? ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Call base OnStartup first to ensure standard WPF application startup initialization is done.
            base.OnStartup(e);

            // Register Global Exception Handling
            GlobalExceptionHandler.Register();

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "client-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // Configure DI container
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Initialize Local Admin database (ensure default credentials/files are set up)
            try
            {
                var adminService = ServiceProvider.GetRequiredService<ILocalAdminService>();
                // Safely execute async init synchronously during startup to prevent UI race conditions
                Task.Run(async () => await adminService.InitializeAdmin()).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize local admin service.");
            }

            // Load and apply workstation configuration
            try
            {
                var configService = ServiceProvider.GetRequiredService<IClientConfigurationService>();
                var configModel = Task.Run(async () => await configService.GetConfigurationAsync()).GetAwaiter().GetResult();

                Log.Information("Workstation configuration successfully loaded. Server Endpoint: {ServerIp}:{Port}, AutoDiscovery: {AutoDiscovery}, KioskMode: {KioskMode}, Language: {Language}",
                    configModel.ServerDiscovery.ServerIp,
                    configModel.ServerDiscovery.UdpPort,
                    configModel.ServerDiscovery.AutoDiscovery,
                    configModel.LocalPreferences.IsKioskMode,
                    configModel.LocalPreferences.Language);

                if (configModel.LocalPreferences.IsKioskMode)
                {
                    var kioskManager = ServiceProvider.GetRequiredService<KioskManager>();
                    kioskManager.Lockdown();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve or apply local client configuration on startup.");
            }

            // Use OnExplicitShutdown to prevent automatic application exit during the Login to Dashboard transition.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Add configuration
            var config = new ConfigurationBuilder().Build();
            services.AddSingleton<IConfiguration>(config);

            // Add Serilog Logging
            services.AddLogging(builder =>
            {
                builder.AddSerilog(dispose: true);
            });

            // Add ReconnectManager
            services.AddSingleton<ReconnectManager>();

            // Add ClientStateManager
            services.AddSingleton<ClientStateManager>();

            // Add Security Services
            services.AddSingleton<SessionKeyManager>();
            services.AddSingleton<EncryptionManager>();
            services.AddSingleton<IntegrityValidator>();
            services.AddSingleton<AuthManager>();
            services.AddSingleton<SecureTransportLayer>();

            // Add Discovery Service Stub
            services.AddSingleton<Sayra.Client.Discovery.Services.IDiscoveryService, StubDiscoveryService>();

            // Add Command system stubs for MessageHandler
            services.AddSingleton<CommandRouter>();
            services.AddSingleton<MessageHandler>();

            // Add Local Admin Services & Repositories
            services.AddLocalAdmin();

            // Add Kiosk Manager
            services.AddSingleton<KioskManager>();

            // Add Session Manager
            services.AddSingleton<SessionManager>();

            // Add TcpClientManager (Fully supported with valid dependencies)
            services.AddSingleton<TcpClientManager>();

            // Register ViewModels
            services.AddTransient<Sayra.UI.ViewModels.LoginViewModel>();
        }
    }

    public class StubDiscoveryService : Sayra.Client.Discovery.Services.IDiscoveryService
    {
        public Task<Sayra.Client.Discovery.Models.DiscoveryResponse?> DiscoverAsync(System.Threading.CancellationToken cancellationToken, bool forceFresh)
        {
            return Task.FromResult<Sayra.Client.Discovery.Models.DiscoveryResponse?>(null);
        }
    }
}
