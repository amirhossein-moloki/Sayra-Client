using CommunityToolkit.Mvvm.ComponentModel;
using Sayra.Client.UI.Services;
using System;

namespace Sayra.Client.UI.ViewModels
{
    public partial class SessionViewModel : ObservableObject, IDisposable
    {
        private readonly IClientBridge _clientBridge;
        private readonly TimerService _timerService;
        private readonly WarningOverlayService _warningService;
        private readonly System.Reactive.Disposables.CompositeDisposable _disposables = new();

        [ObservableProperty]
        private string _remainingTimeStr = "00:00:00";

        [ObservableProperty]
        private string _elapsedTimeStr = "00:00:00";

        [ObservableProperty]
        private string _startTimeStr = "--:--";

        [ObservableProperty]
        private SessionState _state;

        [ObservableProperty]
        private bool _isLowTime;

        private TimeSpan _remainingTime;
        private double _elapsedSeconds;

        public SessionViewModel(IClientBridge clientBridge, TimerService timerService, WarningOverlayService warningService)
        {
            _clientBridge = clientBridge;
            _timerService = timerService;
            _warningService = warningService;

            _clientBridge.SubscribeToStateChanged().Subscribe(state =>
            {
                State = state.SessionState;
                _remainingTime = state.RemainingTime;
                _elapsedSeconds = state.ElapsedSeconds;

                StartTimeStr = state.StartTime?.ToLocalTime().ToString("HH:mm") ?? "--:--";
                UpdateDisplay();

                _warningService.UpdateRemainingTime(_remainingTime);
            }).DisposeWith(_disposables);

            _timerService.Ticks.Subscribe(_ =>
            {
                if (State == SessionState.InSession || State == SessionState.SessionEnding)
                {
                    _remainingTime = _remainingTime.Subtract(TimeSpan.FromSeconds(1));
                    if (_remainingTime < TimeSpan.Zero) _remainingTime = TimeSpan.Zero;

                    _elapsedSeconds++;
                    UpdateDisplay();

                    _warningService.UpdateRemainingTime(_remainingTime);
                }
            }).DisposeWith(_disposables);
        }

        private void UpdateDisplay()
        {
            RemainingTimeStr = _remainingTime.ToString(@"hh\:mm\:ss");
            ElapsedTimeStr = TimeSpan.FromSeconds(_elapsedSeconds).ToString(@"hh\:mm\:ss");
            IsLowTime = _remainingTime.TotalMinutes < 10;
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }

    public static class DisposableExtensions
    {
        public static T DisposeWith<T>(this T disposable, System.Reactive.Disposables.CompositeDisposable composite) where T : IDisposable
        {
            composite.Add(disposable);
            return disposable;
        }
    }
}
