using System;
using System.Windows;

namespace Sayra.UI.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            this.Closed += LoginWindow_Closed;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void LoginWindow_Closed(object? sender, EventArgs e)
        {
            // Only shut down if the user closed the window and we are not transitioning to the dashboard
            bool isDashboardOpen = false;
            foreach (Window win in Application.Current.Windows)
            {
                if (win is DashboardWindow)
                {
                    isDashboardOpen = true;
                    break;
                }
            }
            if (!isDashboardOpen)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
