using System;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.UI.Notifications.Services
{
    public interface INotificationDispatcher
    {
        Task DispatchAsync(NotificationPayload notification);
    }
}
