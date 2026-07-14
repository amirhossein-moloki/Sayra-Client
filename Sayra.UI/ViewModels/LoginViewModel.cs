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
                Sayra.UI.Services.NotificationService.Instance.ShowWarning("نام کاربری و رمز عبور را وارد کنید.");
                return;
            }

            if (Username != "admin" || Password != "admin")
            {
                ErrorMessage = "نام کاربری یا رمز عبور اشتباه است.";
                Sayra.UI.Services.NotificationService.Instance.ShowError("نام کاربری یا رمز عبور اشتباه است.");
                return;
            }

            ErrorMessage = string.Empty;
            IsLoggingIn = true;
            bool loginSuccessful = false;

            GlobalExceptionHandler.CurrentOperation = "Authentication started";
            GlobalExceptionHandler.LogTrace("LOGIN", "Authentication started");

            // Show our premium loading notification on the login screen
            Sayra.UI.Services.NotificationService.Instance.ShowLoading("در حال ورود به سیستم...");

            try
            {
                // Simulate network latency / loading animation
                await Task.Delay(1500);
                loginSuccessful = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login failed: {ex.Message}";
                GlobalExceptionHandler.LogTrace("LOGIN", $"Authentication failed: {ex.Message}");
                Sayra.UI.Services.NotificationService.Instance.ShowError($"خطا در ورود: {ex.Message}");
            }
            finally
            {
                IsLoggingIn = false;
            }

            if (loginSuccessful)
            {
                Sayra.UI.Services.NotificationService.Instance.Dismiss();
                GlobalExceptionHandler.LogTrace("LOGIN", "Authentication successful");
                try
                {
                    GlobalExceptionHandler.CurrentOperation = "Creating DashboardWindow";
                    GlobalExceptionHandler.LogTrace("DASHBOARD", "Creating DashboardWindow");

                    var dashboard = new Sayra.UI.Views.DashboardWindow();

                    GlobalExceptionHandler.CurrentOperation = "Showing window";
                    GlobalExceptionHandler.LogTrace("DASHBOARD", "Showing window");

                    dashboard.Show();

                    GlobalExceptionHandler.CurrentOperation = "Window displayed";
                    GlobalExceptionHandler.LogTrace("DASHBOARD", "Window displayed");

                    var oldWindow = Application.Current.MainWindow;
                    Application.Current.MainWindow = dashboard;
                    oldWindow?.Close();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Failed to load dashboard: {ex.Message}";
                    GlobalExceptionHandler.LogTrace("DASHBOARD", $"Dashboard load/show exception: {ex}");
                    GlobalExceptionHandler.HandleException(ex, "Dashboard Creation Flow");
                }
            }
        }
    }
}
