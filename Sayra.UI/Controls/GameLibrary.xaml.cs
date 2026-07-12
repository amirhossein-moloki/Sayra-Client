using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Sayra.UI.Controls
{
    public partial class GameLibrary : UserControl
    {
        public static readonly DependencyProperty GridColumnsProperty =
            DependencyProperty.Register(nameof(GridColumns), typeof(int), typeof(GameLibrary),
                new PropertyMetadata(6));

        public int GridColumns
        {
            get => (int)GetValue(GridColumnsProperty);
            set => SetValue(GridColumnsProperty, value);
        }

        public GameLibrary()
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

            this.Loaded += GameLibrary_Loaded;
            this.SizeChanged += GameLibrary_SizeChanged;
            Log("Constructor END");
            sw.Stop();
            GlobalExceptionHandler.LogTrace("TIMING", $"[GameLibrary] Constructor & InitializeComponent completed in {sw.ElapsedMilliseconds} ms");
        }

        private void GameLibrary_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Calculate dynamic column count based on available width
            // All cards have identical width = 188px
            // Margin="11,14" -> total horizontal slot per card is 188 + (11 * 2) = 210px.
            double cardWidthWithMargin = 210.0;
            double availableWidth = e.NewSize.Width - 10; // 10px safety buffer
            int cols = (int)(availableWidth / cardWidthWithMargin);
            if (cols < 1) cols = 1;
            if (cols > 6) cols = 6; // Max 6 columns as required for a premium layout
            GridColumns = cols;
        }

        private void GameLibrary_Loaded(object sender, RoutedEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            Log("Loaded Event START");
            Log("Loaded Event END");
            sw.Stop();
            GlobalExceptionHandler.LogTrace("TIMING", $"[GameLibrary] Loaded event completed in {sw.ElapsedMilliseconds} ms");
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
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
