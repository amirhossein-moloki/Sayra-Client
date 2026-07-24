using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sayra.Client.Shared.Models;
using Sayra.UI.Notifications.Services;

namespace Sayra.UI.Notifications.ViewModels
{
    public partial class NotificationCardViewModel : ObservableObject
    {
        private readonly NotificationPayload _payload;
        private readonly NotificationAcknowledgementService _ackService;
        private readonly INotificationActionHandler _actionHandler;
        private readonly Action<NotificationCardViewModel> _onDismissed;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _body = string.Empty;

        [ObservableProperty]
        private string _timestampText = string.Empty;

        [ObservableProperty]
        private string _categoryText = string.Empty;

        [ObservableProperty]
        private string _priorityText = string.Empty;

        [ObservableProperty]
        private bool _hasAction;

        [ObservableProperty]
        private string _actionText = string.Empty;

        public NotificationPayload Payload => _payload;

        public NotificationCardViewModel(
            NotificationPayload payload,
            NotificationAcknowledgementService ackService,
            INotificationActionHandler actionHandler,
            Action<NotificationCardViewModel> onDismissed)
        {
            _payload = payload;
            _ackService = ackService;
            _actionHandler = actionHandler;
            _onDismissed = onDismissed;

            Title = payload.Title;
            Body = payload.Body;
            TimestampText = payload.CreatedAt.ToLocalTime().ToString("t");
            CategoryText = payload.Category.ToString();
            PriorityText = payload.Priority.ToString();

            HasAction = !string.IsNullOrEmpty(payload.ActionCallbackToken);
            if (HasAction)
            {
                // Dynamic localization from resource dictionaries with static fallback for testing
                if (System.Windows.Application.Current != null)
                {
                    ActionText = System.Windows.Application.Current.TryFindResource($"Action.{payload.ActionCallbackToken}") as string
                                 ?? (payload.ActionCallbackToken switch
                                 {
                                     "EXTEND_SESSION_1H" or "EXTEND_SESSION" => "تمدید نشست",
                                     "CONFIRM_SHUTDOWN" => "تایید خاموشی",
                                     "OPEN_ADMIN_MESSAGE" => "نمایش پیام",
                                     "ACCEPT_UPDATE" => "قبول بروزرسانی",
                                     _ => "تایید"
                                 });
                }
                else
                {
                    ActionText = payload.ActionCallbackToken switch
                    {
                        "EXTEND_SESSION_1H" or "EXTEND_SESSION" => "تمدید نشست",
                        "CONFIRM_SHUTDOWN" => "تایید خاموشی",
                        "OPEN_ADMIN_MESSAGE" => "نمایش پیام",
                        "ACCEPT_UPDATE" => "قبول بروزرسانی",
                        _ => "تایید"
                    };
                }
            }
        }

        [RelayCommand]
        private async Task ExecuteActionAsync()
        {
            if (HasAction && !string.IsNullOrEmpty(_payload.ActionCallbackToken))
            {
                await _actionHandler.HandleActionAsync(_payload, _payload.ActionCallbackToken);
            }
            Dismiss();
        }

        [RelayCommand]
        private void Dismiss()
        {
            _ackService.ReportDismissedAsync(_payload.Id).ConfigureAwait(false);
            _onDismissed?.Invoke(this);
        }
    }
}
