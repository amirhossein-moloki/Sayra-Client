using System;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Interfaces;

namespace Sayra.UI.Notifications.Services
{
    public class NotificationAcknowledgementService
    {
        private readonly NotificationIpcClient _ipcClient;
        private readonly IAuditLogger? _auditLogger;

        public NotificationAcknowledgementService(NotificationIpcClient ipcClient, IAuditLogger? auditLogger = null)
        {
            _ipcClient = ipcClient;
            _auditLogger = auditLogger;
        }

        public async Task ReportReceivedAsync(Guid notificationId)
        {
            await SendAckInternalAsync(notificationId, NotificationAckStatus.Received);
        }

        public async Task ReportDisplayedAsync(Guid notificationId)
        {
            await SendAckInternalAsync(notificationId, NotificationAckStatus.Displayed);
        }

        public async Task ReportClickedAsync(Guid notificationId, string actionToken)
        {
            await SendAckInternalAsync(notificationId, NotificationAckStatus.Clicked, actionToken);
        }

        public async Task ReportDismissedAsync(Guid notificationId)
        {
            await SendAckInternalAsync(notificationId, NotificationAckStatus.Dismissed);
        }

        public async Task ReportFailureAsync(Guid notificationId, string errorMessage)
        {
            await SendAckInternalAsync(notificationId, NotificationAckStatus.Failed, errorMessage: errorMessage);
        }

        private async Task SendAckInternalAsync(
            Guid notificationId,
            NotificationAckStatus status,
            string? actionToken = null,
            string? errorMessage = null)
        {
            var ack = new NotificationAckPayload
            {
                NotificationId = notificationId,
                Status = status,
                ActionToken = actionToken,
                Timestamp = DateTime.UtcNow,
                ErrorMessage = errorMessage
            };

            // Local auditing if available
            _auditLogger?.LogAudit($"Local UI transition: ID={notificationId}, State={status}");

            // Transmit over IPC to Session 0 (which persists in OfflineQueue and runs actions)
            await _ipcClient.SendAckAsync(ack);
        }
    }
}
