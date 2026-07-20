using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Sayra.UI.Models;
using Sayra.Client.Launcher.Services;
using Sayra.Client.LocalAdmin.Services;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.GameLibrary.Services;
using Sayra.UI.Views;

namespace Sayra.UI.ViewModels
{
    public partial class GameDetailViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private GameItem? _game;

        [ObservableProperty]
        private string _pcName = "Station";

        [ObservableProperty]
        private string _statusText = "آماده بازی";

        [ObservableProperty]
        private Brush? _statusBorderBrush;

        [ObservableProperty]
        private Brush? _statusBackgroundBrush;

        [ObservableProperty]
        private Brush? _statusForegroundBrush;

        [ObservableProperty]
        private Brush? _statusDotBrush;

        [ObservableProperty]
        private ImageSource? _logoImageSource;

        [ObservableProperty]
        private ImageSource? _backgroundImageSource;

        private readonly IGameLauncherService? _launcherService;
        private readonly IStationIdentityService? _stationService;
        private readonly IAuthenticationService? _authService;
        private readonly IGameValidationService? _validationService;

        // Custom action for closing the Window cleanly
        public event Action? CloseRequested;

        // Parameterless constructor for XAML support and design-time fallback
        public GameDetailViewModel() : this(
            null,
            App.ServiceProvider?.GetService<IGameLauncherService>(),
            App.ServiceProvider?.GetService<IStationIdentityService>(),
            App.ServiceProvider?.GetService<IAuthenticationService>(),
            App.ServiceProvider?.GetService<IGameValidationService>())
        {
        }

        // DI-friendly constructor
        public GameDetailViewModel(
            GameItem? game,
            IGameLauncherService? launcherService,
            IStationIdentityService? stationService,
            IAuthenticationService? authService,
            IGameValidationService? validationService)
        {
            _launcherService = launcherService;
            _stationService = stationService;
            _authService = authService;
            _validationService = validationService;

            Log("Constructor START");

            if (game != null)
            {
                Initialize(game);
            }

            SubscribeToLauncherEvents();
            Log("Constructor END");
        }

        public void Dispose()
        {
            if (_launcherService != null)
            {
                try
                {
                    _launcherService.GameStarted -= LauncherService_GameStarted;
                    _launcherService.GameExited -= LauncherService_GameExited;
                    _launcherService.GameCrashed -= LauncherService_GameCrashed;
                    _launcherService.LaunchFailed -= LauncherService_LaunchFailed;
                    Log("Successfully unsubscribed from core launcher lifecycle events.");
                }
                catch (Exception ex)
                {
                    Log($"Failed to unsubscribe launcher events: {ex}");
                }
            }
        }

        public void Initialize(GameItem game)
        {
            Game = game;

            // Resolve workstation identity
            if (_stationService != null)
            {
                try
                {
                    PcName = _stationService.GetIdentity().ResolvedStationName;
                }
                catch (Exception ex)
                {
                    Log($"Failed to resolve Station Name: {ex.Message}");
                }
            }

            UpdateStatusStyle();
            LoadImages();
        }

        private void SubscribeToLauncherEvents()
        {
            if (_launcherService == null) return;
            try
            {
                _launcherService.GameStarted += LauncherService_GameStarted;
                _launcherService.GameExited += LauncherService_GameExited;
                _launcherService.GameCrashed += LauncherService_GameCrashed;
                _launcherService.LaunchFailed += LauncherService_LaunchFailed;
            }
            catch (Exception ex)
            {
                Log($"Failed to subscribe to launcher events in GameDetailViewModel: {ex}");
            }
        }

        private void LauncherService_GameStarted(object? sender, Sayra.Client.Launcher.Events.GameStartedEventArgs e)
        {
            if (Game != null && Game.Id == e.GameId)
            {
                UpdateGameStatusInUI("Currently Playing");
            }
        }

        private void LauncherService_GameExited(object? sender, Sayra.Client.Launcher.Events.GameExitedEventArgs e)
        {
            if (Game != null && Game.Id == e.GameId)
            {
                UpdateGameStatusInUI("Installed");
            }
        }

        private void LauncherService_GameCrashed(object? sender, Sayra.Client.Launcher.Events.GameCrashedEventArgs e)
        {
            if (Game != null && Game.Id == e.GameId)
            {
                UpdateGameStatusInUI("Installed");
            }
        }

        private void LauncherService_LaunchFailed(object? sender, Sayra.Client.Launcher.Events.LaunchFailedEventArgs e)
        {
            if (Game != null && Game.Id == e.GameId)
            {
                UpdateGameStatusInUI("Installed");
            }
        }

        private void UpdateGameStatusInUI(string status)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (Game != null)
                {
                    Game.Status = status;
                    UpdateStatusStyle();
                }
            });
        }

        private void UpdateStatusStyle()
        {
            if (Game == null) return;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    string status = Game.Status;
                    string upperStatus = (status ?? "").ToUpperInvariant();

                    if (upperStatus == "CURRENTLY PLAYING" || upperStatus == "PLAYING" || upperStatus == "RUNNING")
                    {
                        StatusBorderBrush = (Brush)Application.Current.FindResource("GameDetail.Status.SuccessBorder");
                        StatusBackgroundBrush = (Brush)Application.Current.FindResource("GameDetail.Status.SuccessBackground");
                        StatusForegroundBrush = (Brush)Application.Current.FindResource("GameDetail.Status.SuccessBorder");
                        StatusDotBrush = (Brush)Application.Current.FindResource("GameDetail.Status.SuccessBorder");
                        StatusText = "در حال بازی";
                    }
                    else if (upperStatus == "LOCKED" || upperStatus == "UNAVAILABLE" || upperStatus == "DISABLED")
                    {
                        StatusBorderBrush = (Brush)Application.Current.FindResource("GameDetail.Status.DangerBorder");
                        StatusBackgroundBrush = (Brush)Application.Current.FindResource("GameDetail.Status.DangerBackground");
                        StatusForegroundBrush = (Brush)Application.Current.FindResource("GameDetail.Status.DangerBorder");
                        StatusDotBrush = (Brush)Application.Current.FindResource("GameDetail.Status.DangerBorder");
                        StatusText = upperStatus == "LOCKED" ? "قفل شده" : "غیر فعال";
                    }
                    else
                    {
                        StatusBorderBrush = (Brush)Application.Current.FindResource("GameDetail.Status.WarningBorder");
                        StatusBackgroundBrush = (Brush)Application.Current.FindResource("GameDetail.Status.WarningBackground");
                        StatusForegroundBrush = (Brush)Application.Current.FindResource("GameDetail.Status.WarningBorder");
                        StatusDotBrush = (Brush)Application.Current.FindResource("GameDetail.Status.WarningBorder");
                        StatusText = "آماده بازی";
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to update status style: {ex.Message}");
                }
            });
        }

        private void LoadImages()
        {
            if (Game == null) return;

            // Load Logo
            if (!string.IsNullOrEmpty(Game.LogoImage))
            {
                LogoImageSource = CreateBitmap(Game.LogoImage);
            }

            // Load Background
            if (!string.IsNullOrEmpty(Game.BackgroundImage))
            {
                BackgroundImageSource = CreateBitmap(Game.BackgroundImage);
            }
        }

        private BitmapImage? CreateBitmap(string path)
        {
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
                    bitmap.UriSource = !System.IO.File.Exists(fullPath)
                        ? new Uri(path, UriKind.RelativeOrAbsolute)
                        : new Uri(fullPath, UriKind.Absolute);
                }
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Log($"Failed to load bitmap from path '{path}': {ex.Message}");
                return null;
            }
        }

        [RelayCommand]
        private async Task PlayGameAsync()
        {
            if (Game == null) return;

            if (Game.Status == "Currently Playing")
            {
                ShowNotification("سیستم سایرا", "این بازی در حال حاضر در حال اجراست.");
                return;
            }

            if (_launcherService != null)
            {
                Log($"Invoking core launcher service for game: {Game.Title} (Id: {Game.Id})");
                bool success = await _launcherService.LaunchGameAsync(Game.Id);
                if (!success)
                {
                    Log($"Failed to launch game via Core Launcher: {Game.Title}");
                    ShowNotification("خطای اجرا", $"خطا در اجرای بازی {Game.Title}");
                }
            }
            else
            {
                Log($"Mock launch for game: {Game.Title}");
                UpdateGameStatusInUI("Currently Playing");

                _ = Task.Delay(10000).ContinueWith(_ =>
                {
                    UpdateGameStatusInUI("Installed");
                    ShowNotification("سیستم سایرا", $"بازی {Game.Title} بسته شد.");
                });
            }

            // Close Game Detail View when playing to let focus stay on dashboard/launcher
            CloseRequested?.Invoke();
        }

        [RelayCommand]
        private async Task EndSessionAsync()
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
                ShowLoadingNotification("در حال خروج از سیستم...");

                if (_authService != null)
                {
                    await _authService.LogoutAsync();
                }

                await Task.Delay(1000);
                DismissNotification();

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var loginWin = new LoginWindow();
                    loginWin.Show();
                    Application.Current.MainWindow = loginWin;
                    CloseRequested?.Invoke();
                });
            }
        }

        [RelayCommand]
        private void Back()
        {
            CloseRequested?.Invoke();
        }

        private void ShowNotification(string title, string message)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    Sayra.UI.Services.NotificationService.Instance.ShowSuccess(message);
                }
                catch { }
            });
        }

        private void ShowLoadingNotification(string message)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    Sayra.UI.Services.NotificationService.Instance.ShowLoading(message);
                }
                catch { }
            });
        }

        private void DismissNotification()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    Sayra.UI.Services.NotificationService.Instance.Dismiss();
                }
                catch { }
            });
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][GameDetailViewModel][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}