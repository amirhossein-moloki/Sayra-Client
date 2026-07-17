using System.Windows;

namespace Sayra.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Register Global Exception Handling
            GlobalExceptionHandler.Register();

            base.OnStartup(e);

            // Use OnExplicitShutdown to prevent automatic application exit during the Login to Dashboard transition.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                GlobalExceptionHandler.LogTrace("STARTUP", "Initializing and displaying LoginWindow programmatically...");
                var loginWindow = new Sayra.UI.Views.LoginWindow();
                this.MainWindow = loginWindow;
                loginWindow.Show();
                GlobalExceptionHandler.LogTrace("STARTUP", "LoginWindow displayed successfully.");
            }
            catch (System.Exception ex)
            {
                GlobalExceptionHandler.LogTrace("STARTUP_CRITICAL", $"Failed to initialize or display LoginWindow: {ex.Message}");
                GlobalExceptionHandler.HandleException(ex, "App OnStartup Programmatic Window Initialization");
                this.Shutdown();
            }
        }
    }
}
