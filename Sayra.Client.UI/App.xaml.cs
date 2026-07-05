using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.UI.Services;
using Sayra.Client.UI.ViewModels;

namespace Sayra.Client.UI
{
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        public App()
        {
            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Services
            services.AddSingleton<IClientBridge, MockClientBridge>();

            // ViewModels
            services.AddSingleton<ShellViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<LauncherViewModel>();
            services.AddTransient<SessionViewModel>();

            // Windows
            services.AddSingleton<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var mainWindow = ServiceProvider?.GetRequiredService<MainWindow>();
            mainWindow?.Show();
        }
    }
}
