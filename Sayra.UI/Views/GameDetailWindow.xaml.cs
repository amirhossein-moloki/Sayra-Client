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
        private readonly DashboardWindow _dashboard;

        public GameDetailWindow(GameItem game, DashboardWindow dashboard)
        {
            InitializeComponent();
            _game = game;
            _dashboard = dashboard;

            PopulateDetails();
        }

        private void PopulateDetails()
        {
            if (_game == null) return;

            // Bind values directly
            DetailTitle.Text = _game.Title;
            DetailGenre.Text = _game.Genre;
            DetailDescription.Text = string.IsNullOrEmpty(_game.Description)
                ? "توضیحاتی برای این بازی ثبت نشده است."
                : _game.Description;

            // Status mapping and styling
            string status = _game.Status;
            DetailStatus.Text = status;

            // Update badge color based on active state
            if (!string.IsNullOrEmpty(status))
            {
                string upperStatus = status.ToUpperInvariant();
                if (upperStatus == "CURRENTLY PLAYING" || upperStatus == "PLAYING" || upperStatus == "RUNNING")
                {
                    StatusBadgeBorder.BorderBrush = (Brush)FindResource("SuccessBrush");
                    StatusBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(16, 20, 190, 120)); // Subtle green transparent
                    StatusBadgeDot.Fill = (Brush)FindResource("SuccessBrush");
                    DetailStatus.Foreground = (Brush)FindResource("SuccessBrush");
                    DetailStatus.Text = "در حال بازی";
                }
                else if (upperStatus == "LOCKED" || upperStatus == "UNAVAILABLE")
                {
                    StatusBadgeBorder.BorderBrush = (Brush)FindResource("RedBrush");
                    StatusBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(16, 244, 107, 107)); // Subtle red transparent
                    StatusBadgeDot.Fill = (Brush)FindResource("RedBrush");
                    DetailStatus.Foreground = (Brush)FindResource("RedBrush");
                    DetailStatus.Text = upperStatus == "LOCKED" ? "قفل شده" : "غیر فعال";
                }
                else
                {
                    StatusBadgeBorder.BorderBrush = (Brush)FindResource("PrimaryYellowBrush");
                    StatusBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 61)); // Subtle yellow transparent
                    StatusBadgeDot.Fill = (Brush)FindResource("PrimaryYellowBrush");
                    DetailStatus.Foreground = (Brush)FindResource("PrimaryYellowBrush");
                    DetailStatus.Text = "آماده بازی";
                }
            }

            // Update Play button state
            PlayGameBtn.IsEnabled = _game.IsAvailable;

            // Update large artwork
            UpdateCoverImage();
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
