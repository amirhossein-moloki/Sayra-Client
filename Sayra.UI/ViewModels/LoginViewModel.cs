using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Sayra.Client.LocalAdmin.Services;
using SayraClient.Services;

namespace Sayra.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly ILocalAdminService? _localAdminService;
        private readonly IClientConfigurationService? _clientConfigurationService;
        private readonly SessionManager? _sessionManager;
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
            App.ServiceProvider?.GetService(typeof(ILocalAdminService)) as ILocalAdminService,
            App.ServiceProvider?.GetService(typeof(IClientConfigurationService)) as IClientConfigurationService,
            App.ServiceProvider?.GetService(typeof(SessionManager)) as SessionManager,
            App.ServiceProvider?.GetService(typeof(ILogger<LoginViewModel>)) as ILogger<LoginViewModel>
        )
        {
        }

        public LoginViewModel(
            ILocalAdminService? localAdminService,
            IClientConfigurationService? clientConfigurationService,
            SessionManager? sessionManager,
            ILogger<LoginViewModel>? logger)
        {
            _localAdminService = localAdminService;
            _clientConfigurationService = clientConfigurationService;
            _sessionManager = sessionManager;
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

            bool isValidAmir = Username == "amir" && Password == "amir";
            bool isValidAdmin = false;
            string? localAdminError = null;

            if (Username == "admin" || Username == "afmin")
            {
                if (_localAdminService != null)
                {
                    try
                    {
                        var authResult = await _localAdminService.Authenticate(Username, Password);
                        isValidAdmin = authResult.Success;
                        if (!isValidAdmin)
                        {
                            localAdminError = authResult.ErrorReason;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Local admin authentication service threw an exception.");
                        localAdminError = $"خطای سیستم: {ex.Message}";
                    }
                }
                else
                {
                    // Fallback to static credentials if service is not registered/active (e.g., designer/simple test mode)
                    isValidAdmin = Password == "admin";
                }
            }

            if (!isValidAmir && !isValidAdmin)
            {
                string displayError = localAdminError ?? "نام کاربری یا رمز عبور اشتباه است.";
                ErrorMessage = displayError;
                Sayra.UI.Services.NotificationService.Instance.ShowError(displayError);
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
                App.IsAdminLoggedIn = isValidAdmin;
                try
                {
                    Window targetWindow;
                    if (isValidAmir)
                    {
                        GlobalExceptionHandler.CurrentOperation = "Creating HomeWindow";
                        GlobalExceptionHandler.LogTrace("DASHBOARD", "Creating HomeWindow");

                        // Trigger session startup if session manager is available
                        if (_sessionManager != null)
                        {
                            try
                            {
                                var sessionModel = new SayraClient.Models.SessionModel
                                {
                                    SessionId = Guid.NewGuid().ToString(),
                                    PcId = "LocalPC",
                                    SiteId = "LocalSite",
                                    Duration = 120, // default 2 hours session duration
                                    RatePerHour = 15000, // default rate
                                    StartTime = DateTime.UtcNow
                                };
                                _sessionManager.StartSession(sessionModel);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Failed to start session on SessionManager.");
                            }
                        }

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
        }
    }
}
