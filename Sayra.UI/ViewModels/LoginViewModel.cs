using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Enums;

namespace Sayra.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthenticationService? _authService;
        private readonly ILogger<LoginViewModel>? _logger;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _isLoggingIn;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public LoginViewModel() : this(
            App.ServiceProvider?.GetService(typeof(IAuthenticationService)) as IAuthenticationService,
            App.ServiceProvider?.GetService(typeof(ILogger<LoginViewModel>)) as ILogger<LoginViewModel>
        )
        {
        }

        public LoginViewModel(
            IAuthenticationService? authService,
            ILogger<LoginViewModel>? logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both username and password.";
                Sayra.UI.Services.NotificationService.Instance.ShowWarning("نام کاربری و رمز عبور را وارد کنید.");
                return;
            }

            if (_authService == null)
            {
                ErrorMessage = "سامانه احراز هویت در دسترس نیست.";
                Sayra.UI.Services.NotificationService.Instance.ShowError(ErrorMessage);
                return;
            }

            ErrorMessage = string.Empty;
            IsLoggingIn = true;
            GlobalExceptionHandler.CurrentOperation = "Authentication started";
            GlobalExceptionHandler.LogTrace("LOGIN", "Authentication started");

            // Show our premium loading notification on the login screen
            Sayra.UI.Services.NotificationService.Instance.ShowLoading("در حال ورود به سیستم...");

            try
            {
                // Simulate network latency / loading animation as before
                await Task.Delay(1500);

                // Call the unified core authentication service gateway
                var authResult = await _authService.AuthenticateAsync(Username, Password);

                if (authResult.Success && authResult.AuthenticatedUser != null)
                {
                    Sayra.UI.Services.NotificationService.Instance.Dismiss();
                    GlobalExceptionHandler.LogTrace("LOGIN", "Authentication successful");

                    var role = authResult.AuthenticatedUser.Role;
                    bool isAdmin = (role == UserRole.LocalAdministrator ||
                                    role == UserRole.Administrator ||
                                    role == UserRole.SuperAdministrator);

                    App.IsAdminLoggedIn = isAdmin;

                    try
                    {
                        Window targetWindow;
                        if (!isAdmin)
                        {
                            GlobalExceptionHandler.CurrentOperation = "Creating HomeWindow";
                            GlobalExceptionHandler.LogTrace("DASHBOARD", "Creating HomeWindow");
                            targetWindow = new Sayra.UI.Views.HomeWindow();
                        }
                        else
                        {
                            GlobalExceptionHandler.CurrentOperation = "Creating AdminWindow";
                            GlobalExceptionHandler.LogTrace("DASHBOARD", "Creating AdminWindow");
                            targetWindow = new Sayra.UI.Views.AdminWindow();
                        }

                        GlobalExceptionHandler.CurrentOperation = "Showing window";
                        GlobalExceptionHandler.LogTrace("DASHBOARD", "Showing window");

                        targetWindow.Show();

                        GlobalExceptionHandler.CurrentOperation = "Window displayed";
                        GlobalExceptionHandler.LogTrace("DASHBOARD", "Window displayed");

                        var oldWindow = Application.Current.MainWindow;
                        Application.Current.MainWindow = targetWindow;
                        oldWindow?.Close();
                    }
                    catch (Exception ex)
                    {
                        ErrorMessage = $"Failed to load dashboard: {ex.Message}";
                        GlobalExceptionHandler.LogTrace("DASHBOARD", $"Dashboard load/show exception: {ex}");
                        GlobalExceptionHandler.HandleException(ex, "Dashboard Creation Flow");
                    }
                }
                else
                {
                    string displayError = authResult.FailureReason ?? "نام کاربری یا رمز عبور اشتباه است.";
                    ErrorMessage = displayError;
                    Sayra.UI.Services.NotificationService.Instance.ShowError(displayError);
                    GlobalExceptionHandler.LogTrace("LOGIN", $"Authentication failed: {displayError}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"خطا در ورود: {ex.Message}";
                GlobalExceptionHandler.LogTrace("LOGIN", $"Authentication failed with exception: {ex.Message}");
                Sayra.UI.Services.NotificationService.Instance.ShowError($"خطا در ورود: {ex.Message}");
            }
            finally
            {
                IsLoggingIn = false;
            }
        }
    }
}
