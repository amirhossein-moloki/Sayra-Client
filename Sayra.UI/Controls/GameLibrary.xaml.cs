using System;
using System.Windows;
using System.Windows.Controls;

namespace Sayra.UI.Controls
{
    public partial class GameLibrary : UserControl
    {
        public GameLibrary()
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

            this.Loaded += GameLibrary_Loaded;
            Log("Constructor END");
        }

        private void GameLibrary_Loaded(object sender, RoutedEventArgs e)
        {
            Log("Loaded Event START");
            Log("Loaded Event END");
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][GameLibrary][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}
