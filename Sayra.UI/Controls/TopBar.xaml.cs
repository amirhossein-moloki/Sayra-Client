using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Sayra.UI.Controls
{
    public partial class TopBar : UserControl
    {
        private readonly DispatcherTimer _timer;
        private readonly PersianCalendar _persianCalendar;

        public TopBar()
        {
            var sw = Stopwatch.StartNew();
            Log("Constructor START");
            try
            {
                Log("Before InitializeComponent()");
                InitializeComponent();
                Log("After InitializeComponent() SUCCESS");
            }
            catch (Exception ex)
            {
                Log($"InitializeComponent() FAILED: {ex}");
                throw;
            }

            _persianCalendar = new PersianCalendar();

            Log("Before DispatcherTimer Setup");
            // Initialize and start timer for updating real-time time and date
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Log("DispatcherTimer Started");

            Log("Before initial UpdateDateTime()");
            // Initial update
            UpdateDateTime();
            Log("After initial UpdateDateTime()");

            this.Loaded += TopBar_Loaded;
            this.Unloaded += TopBar_Unloaded;

            Log("Constructor END");
            sw.Stop();
            GlobalExceptionHandler.LogTrace("TIMING", $"[TopBar] Constructor & InitializeComponent completed in {sw.ElapsedMilliseconds} ms");
        }

        private void TopBar_Loaded(object sender, RoutedEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            Log("Loaded Event START");
            try
            {
                AdminPanelButton.Visibility = App.IsAdminLoggedIn ? Visibility.Visible : Visibility.Collapsed;

                var stationService = App.ServiceProvider?.GetService<Sayra.Client.LocalAdmin.Services.IStationIdentityService>();
                if (stationService != null)
                {
                    PcNameText.Text = stationService.GetIdentity().ResolvedStationName;
                }
            }
            catch (Exception ex)
            {
                Log($"Error setting AdminPanelButton or PcNameText: {ex.Message}");
            }
            Log("Loaded Event END");
            sw.Stop();
            GlobalExceptionHandler.LogTrace("TIMING", $"[TopBar] Loaded event completed in {sw.ElapsedMilliseconds} ms");
        }

        private void AdminPanelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GlobalExceptionHandler.LogTrace("TOPBAR_NAV", "Navigating to AdminWindow");
                var adminWin = new Sayra.UI.Views.AdminWindow();
                adminWin.Show();
                Application.Current.MainWindow = adminWin;

                // Close parent HomeWindow
                var parentWindow = Window.GetWindow(this);
                parentWindow?.Close();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.HandleException(ex, "AdminPanel Navigation");
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "آیا مطمئن هستید که می‌خواهید خارج شوید؟",
                    "سیستم سایرا",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
                );

                if (result == MessageBoxResult.Yes)
                {
                    GlobalExceptionHandler.LogTrace("TOPBAR_NAV", "Logging out to LoginWindow");
                    App.IsAdminLoggedIn = false;

                    Sayra.UI.Services.NotificationService.Instance.ShowLoading("در حال خروج از سیستم...");

                    var authService = App.ServiceProvider?.GetService<Sayra.Client.Authentication.Contracts.IAuthenticationService>();
                    if (authService != null)
                    {
                        await authService.LogoutAsync();
                    }

                    await System.Threading.Tasks.Task.Delay(1000);
                    Sayra.UI.Services.NotificationService.Instance.Dismiss();

                    var loginWin = new Sayra.UI.Views.LoginWindow();
                    loginWin.Show();
                    Application.Current.MainWindow = loginWin;

                    // Close parent HomeWindow
                    var parentWindow = Window.GetWindow(this);
                    parentWindow?.Close();
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.HandleException(ex, "Logout Navigation");
            }
        }

        private void TopBar_Unloaded(object sender, RoutedEventArgs e)
        {
            Log("Unloaded Event");
            _timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            Log("Timer_Tick START");
            UpdateDateTime();
            Log("Timer_Tick END");
        }

        private void UpdateDateTime()
        {
            try
            {
                DateTime now = DateTime.Now;

                // Format time as HH:mm
                TimeText.Text = now.ToString("HH:mm");

                // Format date as Persian/Solar Hijri (yyyy/MM/dd)
                int year = _persianCalendar.GetYear(now);
                int month = _persianCalendar.GetMonth(now);
                int day = _persianCalendar.GetDayOfMonth(now);

                DateText.Text = $"{year:D4}/{month:D2}/{day:D2}";
            }
            catch (Exception ex)
            {
                Log($"Date conversion error: {ex.Message}");
            }
        }

        private async void PowerButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "آیا مطمئن هستید که می‌خواهید خارج شوید؟",
                "سیستم سایرا",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
            );

            if (result == MessageBoxResult.Yes)
            {
                Sayra.UI.Services.NotificationService.Instance.ShowLoading("در حال خروج از سیستم...");
                await System.Threading.Tasks.Task.Delay(1000);
                Application.Current.Shutdown();
            }
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][TopBar][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}
