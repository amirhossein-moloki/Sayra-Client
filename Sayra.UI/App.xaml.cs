using System.Windows;

namespace Sayra.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Use OnExplicitShutdown to prevent automatic application exit during the Login to Dashboard transition.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }
    }
}
