using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Sayra.UI.ViewModels;

namespace Sayra.UI.Views.Components
{
    public partial class SessionHero : UserControl
    {
        public SessionHero()
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

            this.Loaded += SessionHero_Loaded;
            Log("Constructor END");
            sw.Stop();
            GlobalExceptionHandler.LogTrace("TIMING", $"[SessionHero] Constructor & InitializeComponent completed in {sw.ElapsedMilliseconds} ms");
        }

        private void SessionHero_Loaded(object sender, RoutedEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            Log("Loaded Event START");
            Log("Loaded Event END");
            sw.Stop();
            GlobalExceptionHandler.LogTrace("TIMING", $"[SessionHero] Loaded event completed in {sw.ElapsedMilliseconds} ms");
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][SessionHero][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}
