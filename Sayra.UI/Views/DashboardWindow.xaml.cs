using System;
using System.Windows;

namespace Sayra.UI.Views
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow()
        {
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

            this.Loaded += DashboardWindow_Loaded;
            this.Closed += DashboardWindow_Closed;
            Log("Constructor END");
        }

        private void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Log("Loaded Event START");
            // Do some lightweight trace or keep it clean
            Log("Loaded Event END");
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
