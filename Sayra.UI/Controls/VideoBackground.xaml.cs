using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Sayra.UI.Controls
{
    public partial class VideoBackground : UserControl
    {
        public VideoBackground()
        {
            InitializeComponent();
            Loaded += VideoBackground_Loaded;
        }

        private void VideoBackground_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the absolute path or pack uri if pack doesn't work under some configurations
            try
            {
                // Fallback attempt to locate file relative to AppDomain if pack fails
                string relativePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "LoginBg.mp4");
                if (File.Exists(relativePath))
                {
                    BackgroundVideo.Source = new Uri(relativePath, UriKind.Absolute);
                }
            }
            catch
            {
                // Ignore fallback exceptions
            }

            BackgroundVideo.Play();
        }

        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Position = TimeSpan.Zero;
            BackgroundVideo.Play();
        }

        private void BackgroundVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // Mute error, can fallback to Solid Color Background
            System.Diagnostics.Debug.WriteLine($"Media failed to play: {e.ErrorException.Message}");
        }
    }
}
