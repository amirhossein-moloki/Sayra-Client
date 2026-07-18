using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sayra.UI.Models;
using Sayra.UI.ViewModels;

namespace Sayra.UI.Views
{
    public partial class GameDetailWindow : Window
    {
        private readonly GameItem _game;
        private readonly HomeWindow _dashboard;

        public GameDetailWindow(GameItem game, HomeWindow dashboard)
        {
            InitializeComponent();
            _game = game;
            _dashboard = dashboard;

            PopulateDetails();
        }

        private void PopulateDetails()
        {
            if (_game == null) return;

            // Bind values directly to window elements
            DetailTitle.Text = _game.Title;
            DetailGenre.Text = _game.Genre;
            BreadcrumbGameTitle.Text = _game.Title;
            DetailDescription.Text = string.IsNullOrEmpty(_game.Description)
                ? "توضیحاتی برای این بازی ثبت نشده است."
                : _game.Description;

            // Rich metadata binding on badges
            DeveloperBadge.Text = string.IsNullOrEmpty(_game.Developer) ? "نا مشخص" : _game.Developer;
            ReleaseYearBadge.Text = string.IsNullOrEmpty(_game.ReleaseYear) ? "نا مشخص" : _game.ReleaseYear;
            LauncherBadge.Text = string.IsNullOrEmpty(_game.Launcher) ? "Custom" : _game.Launcher;

            // Status mapping and styling
            string status = _game.Status;
            DetailStatus.Text = status;

            // Update badge color using dynamic resources
            if (!string.IsNullOrEmpty(status))
            {
                string upperStatus = status.ToUpperInvariant();
                if (upperStatus == "CURRENTLY PLAYING" || upperStatus == "PLAYING" || upperStatus == "RUNNING")
                {
                    StatusBadgeBorder.BorderBrush = (Brush)FindResource("GameDetail.Status.SuccessBorder");
                    StatusBadgeBorder.Background = (Brush)FindResource("GameDetail.Status.SuccessBackground");
                    StatusBadgeDot.Fill = (Brush)FindResource("GameDetail.Status.SuccessBorder");
                    DetailStatus.Foreground = (Brush)FindResource("GameDetail.Status.SuccessBorder");
                    DetailStatus.Text = "در حال بازی";
                }
                else if (upperStatus == "LOCKED" || upperStatus == "UNAVAILABLE")
                {
                    StatusBadgeBorder.BorderBrush = (Brush)FindResource("GameDetail.Status.DangerBorder");
                    StatusBadgeBorder.Background = (Brush)FindResource("GameDetail.Status.DangerBackground");
                    StatusBadgeDot.Fill = (Brush)FindResource("GameDetail.Status.DangerBorder");
                    DetailStatus.Foreground = (Brush)FindResource("GameDetail.Status.DangerBorder");
                    DetailStatus.Text = upperStatus == "LOCKED" ? "قفل شده" : "غیر فعال";
                }
                else
                {
                    StatusBadgeBorder.BorderBrush = (Brush)FindResource("GameDetail.Status.WarningBorder");
                    StatusBadgeBorder.Background = (Brush)FindResource("GameDetail.Status.WarningBackground");
                    StatusBadgeDot.Fill = (Brush)FindResource("GameDetail.Status.WarningBorder");
                    DetailStatus.Foreground = (Brush)FindResource("GameDetail.Status.WarningBorder");
                    DetailStatus.Text = "آماده بازی";
                }
            }

            // Update large artwork, logo, and blurred ambient backdrop
            UpdateCoverImage();
            UpdateLogoImage();
            UpdateBackgroundImage();
        }

        private void UpdateCoverImage()
        {
            if (DetailCoverImage == null) return;

            string path = _game.ImagePath;
            if (string.IsNullOrEmpty(path))
            {
                DetailCoverImage.Source = null;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();

                if (path.StartsWith("pack://") || path.Contains("://"))
                {
                    bitmap.UriSource = new Uri(path);
                }
                else
                {
                    string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                    if (!System.IO.File.Exists(fullPath))
                    {
                        bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                    }
                    else
                    {
                        bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    }
                }

                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                bitmap.Freeze();

                DetailCoverImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameDetailWindow] Error loading image {path}: {ex.Message}");
                DetailCoverImage.Source = null;
            }
        }

        private void UpdateLogoImage()
        {
            if (DetailLogoImage == null) return;

            string path = _game.LogoImage;
            if (string.IsNullOrEmpty(path))
            {
                DetailLogoImage.Source = null;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();

                if (path.StartsWith("pack://") || path.Contains("://"))
                {
                    bitmap.UriSource = new Uri(path);
                }
                else
                {
                    string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                    if (!System.IO.File.Exists(fullPath))
                    {
                        bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                    }
                    else
                    {
                        bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    }
                }

                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                bitmap.Freeze();

                DetailLogoImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameDetailWindow] Error loading logo image {path}: {ex.Message}");
                DetailLogoImage.Source = null;
            }
        }

        private void UpdateBackgroundImage()
        {
            if (DetailBackgroundImage == null) return;

            string path = _game.BackgroundImage;
            if (string.IsNullOrEmpty(path))
            {
                DetailBackgroundImage.Source = null;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();

                if (path.StartsWith("pack://") || path.Contains("://"))
                {
                    bitmap.UriSource = new Uri(path);
                }
                else
                {
                    string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                    if (!System.IO.File.Exists(fullPath))
                    {
                        bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                    }
                    else
                    {
                        bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    }
                }

                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                bitmap.Freeze();

                DetailBackgroundImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameDetailWindow] Error loading background image {path}: {ex.Message}");
                DetailBackgroundImage.Source = null;
            }
        }

        private void DetailCoverImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            try
            {
                if (sender is Image img)
                {
                    img.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                // Suppress failure handling errors
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            // Trigger command on GameLib VM
            try
            {
                if (_dashboard.GameLib?.DataContext is GameLibraryViewModel vm)
                {
                    if (vm.PlayGameCommand != null && vm.PlayGameCommand.CanExecute(_game))
                    {
                        vm.PlayGameCommand.Execute(_game);
                    }
                }
                this.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameDetailWindow] Failed to launch game: {ex.Message}");
            }
        }
    }
}
