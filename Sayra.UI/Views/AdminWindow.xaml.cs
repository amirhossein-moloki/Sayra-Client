using System;
using System.Windows;

namespace Sayra.UI.Views
{
    public partial class AdminWindow : Window
    {
        public AdminWindow()
        {
            InitializeComponent();
            this.Closed += AdminWindow_Closed;
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Sayra.UI.Services.NotificationService.Instance.ShowLoading("در حال خروج از پنل مدیریت...");
                await System.Threading.Tasks.Task.Delay(1000);
                Sayra.UI.Services.NotificationService.Instance.Dismiss();

                var loginWindow = new LoginWindow();
                loginWindow.Show();

                var oldWindow = Application.Current.MainWindow;
                Application.Current.MainWindow = loginWindow;

                // Unregister Closed event to avoid closing the entire application
                this.Closed -= AdminWindow_Closed;
                this.Close();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.HandleException(ex, "Admin Panel Logout");
            }
        }

        private void AdminWindow_Closed(object? sender, EventArgs e)
        {
            // If the user closed the window directly, shut down the application
            bool isLoginOpen = false;
            foreach (Window win in Application.Current.Windows)
            {
                if (win is LoginWindow)
                {
                    isLoginOpen = true;
                    break;
                }
            }
            if (!isLoginOpen)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
