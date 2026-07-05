using CommunityToolkit.Mvvm.ComponentModel;
using Sayra.Client.UI.Services;
using System;

namespace Sayra.Client.UI.ViewModels
{
    public partial class ShellViewModel : ObservableObject
    {
        private readonly IClientBridge _clientBridge;

        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        [ObservableProperty]
        private ClientStatus _status;

        [ObservableProperty]
        private SessionState _sessionState;

        [ObservableProperty]
        private string _userName = "Guest";

        public SessionViewModel Session { get; }

        public ShellViewModel(IClientBridge clientBridge, SessionViewModel sessionViewModel)
        {
            _clientBridge = clientBridge;
            Session = sessionViewModel;

            _clientBridge.SubscribeToStateChanged().Subscribe(state =>
            {
                Status = state.Status;
                SessionState = state.SessionState;
                UserName = state.UserName ?? "Guest";

                UpdateNavigation(state);
            });
        }

        private void UpdateNavigation(ClientState state)
        {
            if (state.SessionState == SessionState.Active)
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
    }
}
