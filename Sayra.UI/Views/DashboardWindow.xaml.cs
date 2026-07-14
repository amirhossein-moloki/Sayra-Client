using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Sayra.UI.Views
{
    public partial class DashboardWindow : Window
    {
        // Stubs to preserve backward compatibility and ensure compilation of GameCard & GameDetailWindow
        public UserControl? GameLib => null;

        public void OpenGameDetail(Sayra.UI.Models.GameItem game)
        {
            // Stub implementation
        }

        public DashboardWindow()
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

            GlobalExceptionHandler.CurrentOperation = "ViewModel creation started";
            GlobalExceptionHandler.LogTrace("DASHBOARD", "ViewModel creation started");

            GlobalExceptionHandler.CurrentOperation = "ViewModel assigned";
            GlobalExceptionHandler.LogTrace("DASHBOARD", "ViewModel assigned");

            this.Loaded += DashboardWindow_Loaded;
            this.Closed += DashboardWindow_Closed;

            swConstructor.Stop();
            GlobalExceptionHandler.LogTrace("DASHBOARD", $"DashboardWindow constructor completed in {swConstructor.ElapsedMilliseconds} ms");
        }

        private void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var swLoaded = Stopwatch.StartNew();
            GlobalExceptionHandler.LogTrace("DASHBOARD", "Loaded Event START");

            Log("Loaded Event START");
            Log("Loaded Event END");

            Sayra.UI.Services.NotificationService.Instance.ShowSuccess("ورود با موفقیت انجام شد!");

            swLoaded.Stop();
            GlobalExceptionHandler.LogTrace("DASHBOARD", $"Loaded Event completed in {swLoaded.ElapsedMilliseconds} ms");
        }

        private void DashboardWindow_Closed(object? sender, EventArgs e)
        {
            Log("Closed Event - Shutting down application...");
            Application.Current.Shutdown();
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][DashboardWindow][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}
