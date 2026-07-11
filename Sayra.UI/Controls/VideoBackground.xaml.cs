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
            Log("[VideoBackground] Initializing Custom Control and MediaElement...");
            InitializeComponent();
            Log("[VideoBackground] MediaElement component initialized.");

            Loaded += VideoBackground_Loaded;
            Unloaded += VideoBackground_Unloaded;
        }

        private void VideoBackground_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("[VideoBackground] Control unloaded. Stopping and cleaning up MediaElement...");
                BackgroundVideo.Stop();
                BackgroundVideo.Source = null;
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
            Log("[VideoBackground] Control loaded. Scheduling asynchronous video initialization...");

            // Use Dispatcher.InvokeAsync with ApplicationIdle priority to allow the Login UI to render and show immediately.
            // This ensures startup remains absolutely non-blocking and responsive.
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await InitializeVideoAsync();
                }
                catch (Exception ex)
                {
                    Log($"[VideoBackground] Unhandled exception during async video setup: {ex.Message}");
                }
            }, DispatcherPriority.ApplicationIdle);
        }

        private async Task InitializeVideoAsync()
        {
            string videoName = VideoName;
            Log($"[VideoBackground] Initializing video using configured name: {videoName}");

            // Run file checks on a background thread to prevent any IO block on UI thread
            string? resolvedPath = await Task.Run(() =>
            {
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    Log($"[VideoBackground] AppDomain BaseDirectory: {baseDir}");

                    // Priority 1: Check in base directory Assets/
                    string path1 = Path.Combine(baseDir, "Assets", videoName);
                    if (File.Exists(path1))
                    {
                        return path1;
                    }

                    // Priority 2: Check directly in base directory
                    string path2 = Path.Combine(baseDir, videoName);
                    if (File.Exists(path2))
                    {
                        return path2;
                    }

                    // Priority 3: Fallback pack URI check or other locations if needed.
                    Log($"[VideoBackground] Video file '{videoName}' not found in build assets.");
                }
                catch (Exception ex)
                {
                    Log($"[VideoBackground] File system check failed: {ex.Message}");
                }
                return null;
            });

            if (string.IsNullOrEmpty(resolvedPath))
            {
                Log($"[VideoBackground] Warning: {videoName} could not be located. Continuing with static/cinematic dark overlay background.");
                return;
            }

            try
            {
                // Log before and after Source assignment
                Log($"[VideoBackground] Setting Source... Target path: {resolvedPath}");
                BackgroundVideo.Source = new Uri(resolvedPath, UriKind.Absolute);
                Log("[VideoBackground] Source assigned successfully.");

                // Log before and after Play() invocation
                Log("[VideoBackground] Invoking Play() on MediaElement...");
                BackgroundVideo.Play();
                Log("[VideoBackground] Play() invoked.");
            }
            catch (Exception ex)
            {
                Log($"[VideoBackground] Error during video source assignment/playback: {ex.Message}");
                Log("[VideoBackground] Continuing with static background fallback.");
            }
        }

        private void BackgroundVideo_MediaOpened(object sender, RoutedEventArgs e)
        {
            // Log on MediaOpened
            Log("[VideoBackground] MediaOpened event triggered. Video is successfully loaded and ready for rendering.");
        }

        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("[VideoBackground] Video reached end. Rewinding to start...");
                BackgroundVideo.Position = TimeSpan.Zero;
                BackgroundVideo.Play();
            }
            catch (Exception ex)
            {
                Log($"[VideoBackground] Error on rewinding/replaying video: {ex.Message}");
            }
        }

        private void BackgroundVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Log($"[VideoBackground] MediaFailed event triggered! Error: {e.ErrorException?.Message ?? "Unknown Media Foundation/Decoding error"}");
            Log("[VideoBackground] Media playback failed. Application will fall back to static/cinematic dark overlay background.");
        }

        private void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
            Console.WriteLine(message);
        }
    }
}
