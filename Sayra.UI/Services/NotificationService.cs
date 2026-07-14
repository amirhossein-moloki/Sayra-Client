using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.UI.Services;

public enum NotificationType
{
    Success,
    Error,
    Warning,
    Loading
}

public partial class NotificationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private NotificationType _notificationType;
}

public partial class NotificationService : ObservableObject
{
    private static readonly Lazy<NotificationService> _instance = new(() => new NotificationService());
    public static NotificationService Instance => _instance.Value;

    [ObservableProperty]
    private bool _isNotificationVisible;

    [ObservableProperty]
    private NotificationViewModel? _currentNotification;

    private CancellationTokenSource? _autoDismissCts;

    // Direct event to notify the UI layers when a close has been requested (so they can trigger exit animation)
    public event Action<Action>? CloseRequested;

    private NotificationService() { }

    public void ShowSuccess(string message) => Show(message, NotificationType.Success);
    public void ShowError(string message) => Show(message, NotificationType.Error);
    public void ShowWarning(string message) => Show(message, NotificationType.Warning);
    public void ShowLoading(string message) => Show(message, NotificationType.Loading);

    public void Show(string message, NotificationType type)
    {
        // Cancel existing dismissal if any
        _autoDismissCts?.Cancel();
        _autoDismissCts = null;

        var notification = new NotificationViewModel
        {
            Message = message,
            NotificationType = type
        };

        CurrentNotification = notification;
        IsNotificationVisible = true;

        // Auto-dismiss after 3 seconds, unless it's a Loading notification
        if (type != NotificationType.Loading)
        {
            _autoDismissCts = new CancellationTokenSource();
            var token = _autoDismissCts.Token;
            Task.Delay(TimeSpan.FromSeconds(3), token).ContinueWith(t =>
            {
                if (!t.IsCanceled && IsNotificationVisible)
                {
                    // Execute on UI thread
                    AppDispatcherInvoke(() => Dismiss());
                }
            }, token);
        }
    }

    public void Dismiss()
    {
        if (!IsNotificationVisible) return;

        _autoDismissCts?.Cancel();
        _autoDismissCts = null;

        // If UI element is registered, let it execute its exit fade animation before closing
        if (CloseRequested != null)
        {
            CloseRequested.Invoke(() =>
            {
                IsNotificationVisible = false;
                CurrentNotification = null;
            });
        }
        else
        {
            IsNotificationVisible = false;
            CurrentNotification = null;
        }
    }

    private void AppDispatcherInvoke(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher != null)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(action);
            }
        }
        else
        {
            action();
        }
    }
}
