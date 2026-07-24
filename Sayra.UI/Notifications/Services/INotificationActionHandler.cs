using System;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.UI.Notifications.Services
{
    public interface INotificationActionHandler
    {
        Task HandleActionAsync(NotificationPayload notification, string actionToken);
    }
}
