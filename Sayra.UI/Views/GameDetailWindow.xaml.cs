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

            // Wire up Play button click event on Hero component
            if (Hero != null && Hero.PlayButtonControl != null)
            {
                Hero.PlayButtonControl.Click += PlayGame_Click;
            }

            PopulateDetails();
        }

        private void PopulateDetails()
        {
            if (_game == null || Hero == null) return;

            // Bind values directly to Hero controls
            Hero.TitleText.Text = _game.Title;
            Hero.GenreText.Text = _game.Genre;
            BreadcrumbGameTitle.Text = _game.Title;
            Hero.DescriptionText.Text = string.IsNullOrEmpty(_game.Description)
                ? "توضیحاتی برای این بازی ثبت نشده است."
                : _game.Description;

            // Rich metadata binding on Hero badges
            Hero.DeveloperBadgeControl.Text = string.IsNullOrEmpty(_game.Developer) ? "نا مشخص" : _game.Developer;
            Hero.ReleaseYearBadgeControl.Text = string.IsNullOrEmpty(_game.ReleaseYear) ? "نا مشخص" : _game.ReleaseYear;
            Hero.LauncherBadgeControl.Text = string.IsNullOrEmpty(_game.Launcher) ? "Custom" : _game.Launcher;

            // Status mapping and styling on Hero status controls
            string status = _game.Status;
            Hero.StatusText.Text = status;

            // Update badge color using dynamic resources
            if (!string.IsNullOrEmpty(status))
            {
                string upperStatus = status.ToUpperInvariant();
                if (upperStatus == "CURRENTLY PLAYING" || upperStatus == "PLAYING" || upperStatus == "RUNNING")
                {
                    Hero.StatusBadgeBorderControl.BorderBrush = (Brush)FindResource("GameDetail.Status.SuccessBorder");
                    Hero.StatusBadgeBorderControl.Background = (Brush)FindResource("GameDetail.Status.SuccessBackground");
                    Hero.StatusBadgeDotControl.Fill = (Brush)FindResource("GameDetail.Status.SuccessBorder");
                    Hero.StatusText.Foreground = (Brush)FindResource("GameDetail.Status.SuccessBorder");
                    Hero.StatusText.Text = "در حال بازی";
                }
                else if (upperStatus == "LOCKED" || upperStatus == "UNAVAILABLE")
                {
                    Hero.StatusBadgeBorderControl.BorderBrush = (Brush)FindResource("GameDetail.Status.DangerBorder");
                    Hero.StatusBadgeBorderControl.Background = (Brush)FindResource("GameDetail.Status.DangerBackground");
                    Hero.StatusBadgeDotControl.Fill = (Brush)FindResource("GameDetail.Status.DangerBorder");
                    Hero.StatusText.Foreground = (Brush)FindResource("GameDetail.Status.DangerBorder");
                    Hero.StatusText.Text = upperStatus == "LOCKED" ? "قفل شده" : "غیر فعال";
                }
                else
                {
                    Hero.StatusBadgeBorderControl.BorderBrush = (Brush)FindResource("GameDetail.Status.WarningBorder");
                    Hero.StatusBadgeBorderControl.Background = (Brush)FindResource("GameDetail.Status.WarningBackground");
                    Hero.StatusBadgeDotControl.Fill = (Brush)FindResource("GameDetail.Status.WarningBorder");
                    Hero.StatusText.Foreground = (Brush)FindResource("GameDetail.Status.WarningBorder");
                    Hero.StatusText.Text = "آماده بازی";
                }
            }

            // Update large artwork, logo, and blurred ambient backdrop on Hero
            UpdateCoverImage();
            UpdateLogoImage();
            UpdateBackgroundImage();
        }

        private void UpdateCoverImage()
        {
            if (Hero?.CoverImage == null) return;

            string path = _game.ImagePath;
            if (string.IsNullOrEmpty(path))
            {
                Hero.CoverImage.Source = null;
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

                Hero.CoverImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameDetailWindow] Error loading image {path}: {ex.Message}");
                Hero.CoverImage.Source = null;
            }
        }

        private void UpdateLogoImage()
        {
            if (Hero?.LogoImage == null) return;

            string path = _game.LogoImage;
            if (string.IsNullOrEmpty(path))
            {
                Hero.LogoImage.Source = null;
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

                Hero.LogoImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameDetailWindow] Error loading logo image {path}: {ex.Message}");
                Hero.LogoImage.Source = null;
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
