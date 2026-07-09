using CommunityToolkit.Mvvm.ComponentModel;
using Sayra.Client.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Sayra.Client.UI.Models;

namespace Sayra.Client.UI.ViewModels
{
    public partial class ShellViewModel : ObservableObject, IDisposable
    {
        private readonly IClientBridge _clientBridge;
        private readonly System.Reactive.Disposables.CompositeDisposable _disposables = new();

        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        [ObservableProperty]
        private ClientStatus _status;

        [ObservableProperty]
        private SessionState _sessionState;

        [ObservableProperty]
        private string _userName = "Guest";

        public SessionViewModel Session { get; }
        public BillingViewModel Billing { get; }
        public WarningOverlayService Warnings { get; }
        public ObservableCollection<GameModel> Games { get; } = new();
        public ObservableCollection<SystemInfoModel> SystemInfo { get; } = new();

        [ObservableProperty]
        private string _clockText = DateTime.Now.ToString("HH:mm");

        [ObservableProperty]
        private string _dateText = "1405/04/15";

        [ObservableProperty]
        private string _remainingTimeText = "00:58:16";

        public ICommand ShutdownCommand { get; }
        public ICommand PowerCommand { get; }

        public ShellViewModel(
            IClientBridge clientBridge,
            SessionViewModel sessionViewModel,
            BillingViewModel billingViewModel,
            WarningOverlayService warningService)
        {
            _clientBridge = clientBridge;
            Session = sessionViewModel;
            Billing = billingViewModel;
            Warnings = warningService;
            ShutdownCommand = new RelayCommand(() => Warnings.ShowSoftWarning("Session termination requested"));
            PowerCommand = new RelayCommand(() => Warnings.ShowSoftWarning("Power action requested"));
            SeedDashboard();
            var uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            var remaining = TimeSpan.FromSeconds(3496);
            uiTimer.Tick += (_, _) =>
            {
                ClockText = DateTime.Now.ToString("HH:mm");
                if (remaining.TotalSeconds > 0) remaining = remaining.Subtract(TimeSpan.FromSeconds(1));
                RemainingTimeText = remaining.ToString(@"hh\:mm\:ss");
            };
            uiTimer.Start();

            _clientBridge.SubscribeToStateChanged().Subscribe(state =>
            {
                Status = state.Status;
                SessionState = state.SessionState;
                UserName = state.UserName ?? "Guest";

                UpdateNavigation(state);
            }).DisposeWith(_disposables);
        }

        private void SeedDashboard()
        {
            Games.Add(new GameModel { Title = "VALORANT", Category = "Shooter", Cover = "pack://application:,,,/Assets/photo_2026-07-08_22-07-56.jpg", State = GameState.Playing });
            Games.Add(new GameModel { Title = "FORTNITE", Category = "Battle Royale", Cover = "pack://application:,,,/Assets/photo_2026-07-08_22-07-56.jpg", State = GameState.Available });
            Games.Add(new GameModel { Title = "CYBERPUNK", Category = "RPG", Cover = "pack://application:,,,/Assets/photo_2026-07-08_22-07-56.jpg", State = GameState.Available });
            Games.Add(new GameModel { Title = "CS2", Category = "Tactical FPS", Cover = "pack://application:,,,/Assets/photo_2026-07-08_22-07-56.jpg", State = GameState.Available });
            Games.Add(new GameModel { Title = "DOTA 2", Category = "MOBA", Cover = "pack://application:,,,/Assets/photo_2026-07-08_22-07-56.jpg", State = GameState.Unavailable });
            SystemInfo.Add(new SystemInfoModel { Icon = "◉", Label = "CPU", Value = "I7 13500F" });
            SystemInfo.Add(new SystemInfoModel { Icon = "◆", Label = "GPU", Value = "RTX4090" });
            SystemInfo.Add(new SystemInfoModel { Icon = "▣", Label = "RAM", Value = "32GB" });
            SystemInfo.Add(new SystemInfoModel { Icon = "▰", Label = "DISPLAY", Value = "4K OLED" });
        }

        private void UpdateNavigation(ClientState state)
        {
            if (state.SessionState == SessionState.InSession || state.SessionState == SessionState.SessionEnding)
            {
                if (CurrentViewModel is not LauncherViewModel)
                {
                    CurrentViewModel = App.ServiceProvider?.GetService(typeof(LauncherViewModel)) as ObservableObject;
                }
            }
            else
            {
                if (CurrentViewModel is not LoginViewModel)
                {
                    CurrentViewModel = App.ServiceProvider?.GetService(typeof(LoginViewModel)) as ObservableObject;
                }
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
