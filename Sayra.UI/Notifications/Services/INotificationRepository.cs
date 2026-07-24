using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.UI.Notifications.Services
{
    public interface INotificationRepository
    {
        Task InitializeAsync();
        Task SaveNotificationAsync(NotificationPayload notification);
        Task<List<NotificationPayload>> GetNotificationsAsync(string? searchQuery = null, NotificationPriority? priorityFilter = null, NotificationCategory? categoryFilter = null);
        Task MarkAsReadAsync(Guid id);
        Task MarkAllAsReadAsync();
        Task DeleteNotificationAsync(Guid id);
        Task ClearAllAsync();
    }
}
