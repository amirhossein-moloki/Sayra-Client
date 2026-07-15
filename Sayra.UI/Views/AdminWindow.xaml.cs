using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sayra.UI.Models;
using Sayra.UI.ViewModels;

namespace Sayra.UI.Views
{
    public partial class AdminWindow : Window
    {
        public AdminWindow()
        {
            InitializeComponent();
            DataContext = new AdminWorkspaceViewModel();
            this.Closed += AdminWindow_Closed;
        }

        private void AppsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid && grid.SelectedItem is AdminAppItem selectedItem)
            {
                // Double click automatically executes the Launch Command
                if (DataContext is AdminWorkspaceViewModel vm)
                {
                    vm.LaunchCommand.Execute(selectedItem);
                }
            }
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
