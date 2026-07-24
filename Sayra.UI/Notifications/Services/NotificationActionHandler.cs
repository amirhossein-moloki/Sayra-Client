using System;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.UI.Notifications.Services
{
    public class NotificationActionHandler : INotificationActionHandler
    {
        private readonly NotificationAcknowledgementService _ackService;

        public NotificationActionHandler(NotificationAcknowledgementService ackService)
        {
            _ackService = ackService;
        }

        public async Task HandleActionAsync(NotificationPayload notification, string actionToken)
        {
            if (notification == null || string.IsNullOrEmpty(actionToken)) return;

            // 1. Validate action token
            if (string.IsNullOrWhiteSpace(notification.ActionCallbackToken) || notification.ActionCallbackToken != actionToken)
            {
                // Action token mismatch or invalid
                await _ackService.ReportFailureAsync(notification.Id, $"Action token validation failed. Received: {actionToken}");
                return;
            }

            // 2. Report Clicked acknowledgement state transition
            await _ackService.ReportClickedAsync(notification.Id, actionToken);
        }
    }
}
