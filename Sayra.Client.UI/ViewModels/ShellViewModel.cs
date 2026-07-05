using CommunityToolkit.Mvvm.ComponentModel;
using Sayra.Client.UI.Services;
using System;

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

            _clientBridge.SubscribeToStateChanged().Subscribe(state =>
            {
                Status = state.Status;
                SessionState = state.SessionState;
                UserName = state.UserName ?? "Guest";

                UpdateNavigation(state);
            }).DisposeWith(_disposables);
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
