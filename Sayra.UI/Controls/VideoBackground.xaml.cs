using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace Sayra.UI.Controls
{
    public partial class VideoBackground : UserControl
    {
        public VideoBackground()
        {
            // Log before and after MediaElement initialization
            Log("[VideoBackground] Initializing Custom Control...");
            InitializeComponent();
            Log("[VideoBackground] Component initialized (Video disabled for debugging).");

            Loaded += VideoBackground_Loaded;
            Unloaded += VideoBackground_Unloaded;
        }

        private void VideoBackground_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("[VideoBackground] Control unloaded (Video is disabled).");
                // BackgroundVideo is disabled to prevent freezes/deadlocks.
            }
            catch (Exception ex)
            {
                Log($"[VideoBackground] Exception during Unloaded cleanup: {ex.Message}");
            }
        }

        public static readonly DependencyProperty VideoNameProperty =
            DependencyProperty.Register(nameof(VideoName), typeof(string), typeof(VideoBackground), new PropertyMetadata("LoginBg.mp4"));

        public string VideoName
        {
            get => (string)GetValue(VideoNameProperty);
            set => SetValue(VideoNameProperty, value);
        }

        public static readonly DependencyProperty OverlayBackgroundProperty =
            DependencyProperty.Register(nameof(OverlayBackground), typeof(Brush), typeof(VideoBackground),
                new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC050507"))));

        public Brush OverlayBackground
        {
            get => (Brush)GetValue(OverlayBackgroundProperty);
            set => SetValue(OverlayBackgroundProperty, value);
        }

        private void VideoBackground_Loaded(object sender, RoutedEventArgs e)
        {
            Log("[VideoBackground] Control loaded. Video playback is disabled to investigate UI freezes.");
        }

        private void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
            Console.WriteLine(message);
        }
    }
}
