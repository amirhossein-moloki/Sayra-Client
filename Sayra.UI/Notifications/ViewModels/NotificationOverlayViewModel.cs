using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Sayra.Client.Shared.Models;
using Sayra.UI.Notifications.Services;

namespace Sayra.UI.Notifications.ViewModels
{
    public partial class NotificationOverlayViewModel : ObservableObject
    {
        private readonly NotificationDispatcher _dispatcher;
        private readonly NotificationAcknowledgementService _ackService;
        private readonly INotificationActionHandler _actionHandler;
        private readonly Dispatcher _uiDispatcher;

        [ObservableProperty]
        private ObservableCollection<NotificationCardViewModel> _activeNotifications = new();

        public NotificationOverlayViewModel(
            NotificationDispatcher dispatcher,
            NotificationAcknowledgementService ackService,
            INotificationActionHandler actionHandler,
            Dispatcher uiDispatcher)
        {
            _dispatcher = dispatcher;
            _ackService = ackService;
            _actionHandler = actionHandler;
            _uiDispatcher = uiDispatcher;

            _dispatcher.DisplayNotificationRequested += OnDisplayNotificationRequested;
        }

        private void OnDisplayNotificationRequested(NotificationPayload payload)
        {
            _uiDispatcher.InvokeAsync(async () =>
            {
                var cardVm = new NotificationCardViewModel(
                    payload,
                    _ackService,
                    _actionHandler,
                    RemoveCard);

                ActiveNotifications.Add(cardVm);
                await _ackService.ReportDisplayedAsync(payload.Id);

                // If on Windows, also dispatch to native Action Center notifications via WindowsNotificationChannel
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        var nativeChannel = new WindowsNotificationChannel();
                        nativeChannel.ShowNotification(payload.Title, payload.Body);
                    }
                    catch
                    {
                        // Fallback gracefully on native notification fail
                    }
                }

                // Auto-dismiss after TTL if set, otherwise default to 6 seconds
                int durationSec = payload.TtlSeconds > 0 ? payload.TtlSeconds : 6;
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(durationSec)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    RemoveCard(cardVm);
                };
                timer.Start();
            });
        }

        private void RemoveCard(NotificationCardViewModel cardVm)
        {
            _uiDispatcher.InvokeAsync(() =>
            {
                if (ActiveNotifications.Contains(cardVm))
                {
                    ActiveNotifications.Remove(cardVm);
                }
            });
        }
    }
}
