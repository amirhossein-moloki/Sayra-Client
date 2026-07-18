using System.Windows;

namespace Sayra.UI
{
    public partial class App : Application
    {
        public static bool IsAdminLoggedIn { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Register Global Exception Handling
            GlobalExceptionHandler.Register();

            base.OnStartup(e);
            // Use OnExplicitShutdown to prevent automatic application exit during the Login to Dashboard transition.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }
    }
}
