using System;
using System.Diagnostics;
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
        private bool _isUnloaded = false;

        public VideoBackground()
        {
            var sw = Stopwatch.StartNew();
            InitializeComponent();
            sw.Stop();
            GlobalExceptionHandler.LogTrace("TIMING", $"[VideoBackground] Constructor & InitializeComponent completed in {sw.ElapsedMilliseconds} ms");

            Loaded += VideoBackground_Loaded;
            Unloaded += VideoBackground_Unloaded;
        }

        private void VideoBackground_Unloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            try
            {
                Log("[VideoBackground] Control unloaded. Cleaning up MediaElement resources.");
                if (BackgroundVideo != null)
                {
                    BackgroundVideo.Stop();
                    BackgroundVideo.Source = null;
                }
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

        private async void VideoBackground_Loaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = false;
            var sw = Stopwatch.StartNew();
            Log("[VideoBackground] Control loaded. Starting asynchronous video initialization...");
            await InitializeVideoAsync();
            sw.Stop();
            GlobalExceptionHandler.LogTrace("TIMING", $"[VideoBackground] Loaded event completed in {sw.ElapsedMilliseconds} ms");
        }

        private async Task InitializeVideoAsync()
        {
            try
            {
                // Defer loading to let UI settle, avoid startup freezes/concurrency deadlocks
                await Task.Delay(300);

                if (_isUnloaded || !this.IsLoaded || BackgroundVideo == null)
                {
                    Log("[VideoBackground] Skipping initialization: Control is unloaded or MediaElement is null.");
                    return;
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string resolvedPath = Path.Combine(baseDir, "Assets", VideoName);

                if (!File.Exists(resolvedPath))
                {
                    Log($"[VideoBackground] Warning: Video file not found at '{resolvedPath}'. Falling back to static background.");
                    return;
                }

                Log($"[VideoBackground] Resolved video path: {resolvedPath}");

                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (_isUnloaded || !this.IsLoaded || BackgroundVideo == null) return;

                        BackgroundVideo.Source = new Uri(resolvedPath, UriKind.Absolute);
                        BackgroundVideo.Play();
                        Log("[VideoBackground] Play() called on MediaElement.");
                    }
                    catch (Exception ex)
                    {
                        Log($"[VideoBackground] Exception during dispatcher-based source assignment: {ex.Message}");
                    }
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Log($"[VideoBackground] Exception during InitializeVideoAsync: {ex.Message}");
            }
        }

        private void BackgroundVideo_MediaOpened(object sender, RoutedEventArgs e)
        {
            Log("[VideoBackground] Media opened successfully.");
        }

        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isUnloaded || !this.IsLoaded || BackgroundVideo == null) return;

                Log("[VideoBackground] Media ended. Looping video...");
                BackgroundVideo.Position = TimeSpan.Zero;
                BackgroundVideo.Play();
            }
            catch (Exception ex)
            {
                Log($"[VideoBackground] Exception during MediaEnded looping: {ex.Message}");
            }
        }

        private void BackgroundVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Log($"[VideoBackground] Media Failed! Error: {e.ErrorException?.Message}");
            // Graceful fallback is already handled by our solid Grid background.
            // Ensure we clean up source to release pipeline.
            try
            {
                if (BackgroundVideo != null)
                {
                    BackgroundVideo.Source = null;
                }
            }
            catch (Exception ex)
            {
                Log($"[VideoBackground] Exception cleaning failed MediaElement: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
            Console.WriteLine(message);
        }
    }
}
