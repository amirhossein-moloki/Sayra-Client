using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace Sayra.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _isLoggingIn;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [RelayCommand]
        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both username and password.";
                return;
            }

            ErrorMessage = string.Empty;
            IsLoggingIn = true;

            try
            {
                // Simulate network latency / loading animation
                await Task.Delay(1500);

                // Show success message and open dashboard
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("ورود با موفقیت انجام شد!", "سیستم سایرا", MessageBoxButton.OK, MessageBoxImage.Information);

                    var dashboard = new Sayra.UI.Views.DashboardWindow();
                    dashboard.Show();

                    var oldWindow = Application.Current.MainWindow;
                    Application.Current.MainWindow = dashboard;
                    oldWindow?.Close();
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login failed: {ex.Message}";
            }
            finally
            {
                IsLoggingIn = false;
            }
        }
    }
}
