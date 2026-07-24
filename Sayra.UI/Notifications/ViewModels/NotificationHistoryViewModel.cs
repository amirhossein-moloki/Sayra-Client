using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sayra.Client.Shared.Models;
using Sayra.UI.Notifications.Services;

namespace Sayra.UI.Notifications.ViewModels
{
    public partial class NotificationHistoryViewModel : ObservableObject
    {
        private readonly INotificationRepository _repository;
        private readonly NotificationAcknowledgementService _ackService;
        private readonly INotificationActionHandler _actionHandler;

        [ObservableProperty]
        private ObservableCollection<NotificationCardViewModel> _notifications = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _selectedPriorityFilter = "All";

        [ObservableProperty]
        private string _selectedCategoryFilter = "All";

        public List<string> Priorities { get; } = new() { "All", "SILENT", "NORMAL", "HIGH", "CRITICAL" };
        public List<string> Categories { get; } = new() { "All", "BILLING", "SYSTEM", "SOCIAL", "ADMINISTRATIVE" };

        public NotificationHistoryViewModel(
            INotificationRepository repository,
            NotificationAcknowledgementService ackService,
            INotificationActionHandler actionHandler)
        {
            _repository = repository;
            _ackService = ackService;
            _actionHandler = actionHandler;

            LoadNotificationsCommand.Execute(null);
        }

        partial void OnSearchQueryChanged(string value) => LoadNotifications();
        partial void OnSelectedPriorityFilterChanged(string value) => LoadNotifications();
        partial void OnSelectedCategoryFilterChanged(string value) => LoadNotifications();

        [RelayCommand]
        private async Task LoadNotificationsAsync()
        {
            NotificationPriority? priority = null;
            if (SelectedPriorityFilter != "All" && Enum.TryParse<NotificationPriority>(SelectedPriorityFilter, out var parsedPriority))
            {
                priority = parsedPriority;
            }

            NotificationCategory? category = null;
            if (SelectedCategoryFilter != "All" && Enum.TryParse<NotificationCategory>(SelectedCategoryFilter, out var parsedCategory))
            {
                category = parsedCategory;
            }

            var results = await _repository.GetNotificationsAsync(SearchQuery, priority, category);

            Notifications.Clear();
            foreach (var item in results)
            {
                Notifications.Add(new NotificationCardViewModel(item, _ackService, _actionHandler, RemoveNotification));
            }
        }

        private void RemoveNotification(NotificationCardViewModel card)
        {
            Notifications.Remove(card);
            _repository.DeleteNotificationAsync(card.Payload.Id).ConfigureAwait(false);
        }

        [RelayCommand]
        private async Task MarkAllAsReadAsync()
        {
            await _repository.MarkAllAsReadAsync();
            await LoadNotificationsAsync();
        }

        [RelayCommand]
        private async Task ClearAllAsync()
        {
            await _repository.ClearAllAsync();
            Notifications.Clear();
        }

        private void LoadNotifications()
        {
            LoadNotificationsCommand.Execute(null);
        }
    }
}
