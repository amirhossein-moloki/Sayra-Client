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
using Sayra.Client.Authentication.Configuration;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.GameLibrary;
using Sayra.Client.Diagnostics.Extensions;
using Sayra.Client.Launcher;
using Sayra.Client.Launcher.Services;
using Sayra.Client.Scanner;

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

            // Hook up authentication core decoupled event listeners
            try
            {
                var authService = ServiceProvider.GetRequiredService<IAuthenticationService>();
                var sessionManager = ServiceProvider.GetRequiredService<SessionManager>();

                authService.AuthenticationSucceeded += (sender, args) =>
                {
                    if (args.User.Role == Sayra.Client.Authentication.Enums.UserRole.Player ||
                        args.User.Role == Sayra.Client.Authentication.Enums.UserRole.Guest)
                    {
                        try
                        {
                            var stationService = ServiceProvider.GetRequiredService<Sayra.Client.LocalAdmin.Services.IStationIdentityService>();
                            var identity = stationService.GetIdentity();

                            var sessionModel = new SayraClient.Models.SessionModel
                            {
                                SessionId = args.SessionId,
                                PcId = identity.ResolvedStationName,
                                SiteId = "LocalSite",
                                Duration = 120, // default 2 hours session duration
                                RatePerHour = 15000, // default rate
                                StartTime = DateTime.UtcNow
                            };
                            sessionManager.StartSession(sessionModel);

                            var stateManager = ServiceProvider.GetRequiredService<ClientStateManager>();
                            stateManager.TransitionTo(ClientState.IN_SESSION);

                            Log.Information("Decoupled session startup triggered for user: {Username}", args.User.Username);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to start session via decoupled authentication event subscription.");
                        }
                    }
                };

                authService.LogoutStarted += (sender, args) =>
                {
                    if (args.User?.Role == Sayra.Client.Authentication.Enums.UserRole.Player ||
                        args.User?.Role == Sayra.Client.Authentication.Enums.UserRole.Guest)
                    {
                        try
                        {
                            var stationService = ServiceProvider.GetRequiredService<Sayra.Client.LocalAdmin.Services.IStationIdentityService>();
                            var identity = stationService.GetIdentity();

                            sessionManager.StopSession(identity.ResolvedStationName);

                            var stateManager = ServiceProvider.GetRequiredService<ClientStateManager>();
                            stateManager.TransitionTo(ClientState.READY);

                            Log.Information("Decoupled session end triggered for user: {Username}", args.User?.Username);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to end session via decoupled logout event subscription.");
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to subscribe to core authentication events during startup.");
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

                // Set language dynamically on startup
                string preferredLang = configModel.LocalPreferences.Language ?? "fa-IR";
                SetLanguage(preferredLang);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve or apply local client configuration on startup.");
                // Fallback to Persian
                SetLanguage("fa-IR");
            }

            // Use OnExplicitShutdown to prevent automatic application exit during the Login to Dashboard transition.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        public static void SetLanguage(string lang)
        {
            var app = Application.Current;
            if (app == null) return;

            // Find existing language dictionary
            ResourceDictionary? existingLangDict = null;
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.Source != null && (dict.Source.OriginalString.Contains("Lang.en.xaml") || dict.Source.OriginalString.Contains("Lang.fa.xaml")))
                {
                    existingLangDict = dict;
                    break;
                }
            }

            if (existingLangDict != null)
            {
                app.Resources.MergedDictionaries.Remove(existingLangDict);
            }

            string sourcePath = lang == "en-US" ? "Themes/Lang.en.xaml" : "Themes/Lang.fa.xaml";
            var newLangDict = new ResourceDictionary { Source = new Uri(sourcePath, UriKind.Relative) };
            app.Resources.MergedDictionaries.Add(newLangDict);
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

            // Add Power, Backup, and Sync Services
            services.AddSingleton<IPowerManagementService, PowerManagementService>();
            services.AddSingleton<IWorkstationBackupService, WorkstationBackupService>();
            services.AddSingleton<IWorkstationSyncService, WorkstationSyncService>();

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

            // Add Unified Authentication Core Services
            services.AddSayraAuthentication();

            // Add Kiosk Manager
            services.AddSingleton<KioskManager>();

            // Add Session Manager
            services.AddSingleton<SessionManager>();
            services.AddSingleton<ISessionStateProvider>(sp => sp.GetRequiredService<SessionManager>());

            // Add TcpClientManager (Fully supported with valid dependencies)
            services.AddSingleton<TcpClientManager>();

            // Add Core GameLibrary, Diagnostics and Launcher Services
            services.AddGameLibrary();
            services.AddDiagnosticsServices(config);
            services.AddLauncherServices();
            services.AddApplicationScanner();

            // Register ViewModels
            services.AddTransient<Sayra.UI.ViewModels.LoginViewModel>();
            services.AddTransient<Sayra.UI.ViewModels.GameLibraryViewModel>();
            services.AddTransient<Sayra.UI.ViewModels.SessionHeroViewModel>();
            services.AddTransient<Sayra.UI.ViewModels.HardwarePanelViewModel>();
            services.AddTransient<Sayra.UI.ViewModels.AdPanelViewModel>();
            services.AddTransient<Sayra.UI.ViewModels.GameDetailViewModel>();
            services.AddTransient<Sayra.UI.ViewModels.AdminWorkspaceViewModel>();
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
