using CommunityToolkit.Mvvm.ComponentModel;
using Sayra.Client.UI.Services;
using System;

namespace Sayra.Client.UI.ViewModels
{
    public partial class SessionViewModel : ObservableObject
    {
        private readonly IClientBridge _clientBridge;

        [ObservableProperty]
        private string _remainingTimeStr = "00:00:00";

        [ObservableProperty]
        private SessionState _state;

        [ObservableProperty]
        private bool _isLowTime;

        public SessionViewModel(IClientBridge clientBridge)
        {
            _clientBridge = clientBridge;

            _clientBridge.SubscribeToStateChanged().Subscribe(state =>
            {
                State = state.SessionState;
                RemainingTimeStr = state.RemainingTime.ToString(@"hh\:mm\:ss");
                IsLowTime = state.RemainingTime.TotalMinutes < 10;
            });
        }
    }
}
