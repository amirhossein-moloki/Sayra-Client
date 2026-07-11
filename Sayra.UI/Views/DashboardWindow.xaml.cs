using System;
using System.Windows;

namespace Sayra.UI.Views
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow()
        {
            InitializeComponent();
            this.Closed += DashboardWindow_Closed;
        }

        private void DashboardWindow_Closed(object? sender, EventArgs e)
        {
            // Explicitly shut down the application when the main Dashboard window is closed
            // to prevent background process leaks under OnExplicitShutdown mode.
            Application.Current.Shutdown();
        }
    }
}
