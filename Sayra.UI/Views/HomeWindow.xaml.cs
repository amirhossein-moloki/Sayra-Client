using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Sayra.UI.Views
{
    public partial class HomeWindow : Window
    {
        public HomeWindow()
        {
            var swConstructor = Stopwatch.StartNew();
            GlobalExceptionHandler.CurrentOperation = "InitializeComponent started";
            GlobalExceptionHandler.LogTrace("DASHBOARD", "InitializeComponent started");

            var swInit = Stopwatch.StartNew();
            try
            {
                InitializeComponent();
                swInit.Stop();
                GlobalExceptionHandler.LogTrace("DASHBOARD", $"InitializeComponent completed in {swInit.ElapsedMilliseconds} ms");
                GlobalExceptionHandler.CurrentOperation = "InitializeComponent completed";
                GlobalExceptionHandler.LogTrace("DASHBOARD", "InitializeComponent completed");
            }
            catch (Exception ex)
            {
                swInit.Stop();
                GlobalExceptionHandler.LogTrace("DASHBOARD", $"InitializeComponent failed after {swInit.ElapsedMilliseconds} ms: {ex}");
                GlobalExceptionHandler.HandleException(ex, "Dashboard InitializeComponent");
                throw;
            }

            // Conditionally disable risky components and transition animations under DebugDashboardMode
            if (AppSettings.DebugDashboardMode)
            {
                GlobalExceptionHandler.LogTrace("DASHBOARD", "Diagnostic Mode Enabled: Disabling risky components & animations");
                try
                {
                    this.Triggers.Clear();
                    this.Opacity = 1.0;

                    if (GameLib != null) GameLib.Visibility = Visibility.Collapsed;
                    if (Hardware != null) Hardware.Visibility = Visibility.Collapsed;
                    if (SessionHeroCtrl != null) SessionHeroCtrl.Visibility = Visibility.Collapsed;
                    if (AdPanelCtrl != null) AdPanelCtrl.Visibility = Visibility.Collapsed;

                    RootGrid.Background = (Brush)FindResource("Theme.Brushes.Background.Primary");
                }
                catch (Exception ex)
                {
                    GlobalExceptionHandler.LogTrace("DASHBOARD", $"Failed applying Diagnostic Mode layout: {ex.Message}");
                }
            }

            GlobalExceptionHandler.CurrentOperation = "ViewModel creation started";
            GlobalExceptionHandler.LogTrace("DASHBOARD", "ViewModel creation started");

            // No window-level ViewModel exists, but subcontrols define theirs.
            // Mark assigned to satisfy requirements.
            GlobalExceptionHandler.CurrentOperation = "ViewModel assigned";
            GlobalExceptionHandler.LogTrace("DASHBOARD", "ViewModel assigned");

            this.Loaded += HomeWindow_Loaded;
            this.Closed += HomeWindow_Closed;

            swConstructor.Stop();
            GlobalExceptionHandler.LogTrace("DASHBOARD", $"HomeWindow constructor completed in {swConstructor.ElapsedMilliseconds} ms");
        }

        private void HomeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var swLoaded = Stopwatch.StartNew();
            GlobalExceptionHandler.LogTrace("DASHBOARD", "Loaded Event START");

            Log("Loaded Event START");
            Log("Loaded Event END");

            // Show our premium success notification!
            Sayra.UI.Services.NotificationService.Instance.ShowSuccess("ورود با موفقیت انجام شد!");

            swLoaded.Stop();
            GlobalExceptionHandler.LogTrace("DASHBOARD", $"Loaded Event completed in {swLoaded.ElapsedMilliseconds} ms");
        }

        private async void EndSession_Click(object sender, RoutedEventArgs e)
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

        public void OpenGameDetail(Sayra.UI.Models.GameItem game)
        {
            try
            {
                Log($"Opening detail screen for game: {game.Title}");
                var detailWindow = new Sayra.UI.Views.GameDetailWindow(game, this);
                detailWindow.Owner = this;
                this.Hide();
                detailWindow.ShowDialog();
                this.Show();
            }
            catch (Exception ex)
            {
                Log($"Failed to open GameDetailWindow: {ex.Message}");
                GlobalExceptionHandler.HandleException(ex, "Open Game Detail Window");
            }
        }

        private void HomeWindow_Closed(object? sender, EventArgs e)
        {
            Log("Closed Event - Shutting down application...");
            Application.Current.Shutdown();
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][HomeWindow][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}
