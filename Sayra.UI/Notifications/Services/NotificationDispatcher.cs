using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.UI.Notifications.Services
{
    public class NotificationDispatcher : INotificationDispatcher
    {
        private readonly INotificationRepository _repository;
        private readonly NotificationAcknowledgementService _ackService;
        private readonly ConcurrentQueue<NotificationPayload> _highPriorityQueue = new();
        private readonly SemaphoreSlim _queueLock = new(1, 1);
        private bool _isProcessingQueue;

        // Hook for UI overlay view model to observe incoming visual alerts
        public event Action<NotificationPayload>? DisplayNotificationRequested;

        public NotificationDispatcher(INotificationRepository repository, NotificationAcknowledgementService ackService)
        {
            _repository = repository;
            _ackService = ackService;
        }

        public async Task DispatchAsync(NotificationPayload notification)
        {
            if (notification == null) return;

            // Step 1: Validate payload and check signature
            string validationError;
            if (!notification.Validate(out validationError))
            {
                await _ackService.ReportFailureAsync(notification.Id, $"Payload validation error: {validationError}");
                return;
            }

            // Check if notification is expired (TTL check)
            if (notification.IsExpired())
            {
                await _ackService.ReportFailureAsync(notification.Id, "Notification expired due to TTL policy.");
                return;
            }

            // Step 2: Save to local Notification History Database (silent notifications included!)
            await _repository.SaveNotificationAsync(notification);
            await _ackService.ReportReceivedAsync(notification.Id);

            // Step 3: Decide Presentation Strategy based on Priority Rules
            switch (notification.Priority)
            {
                case NotificationPriority.CRITICAL:
                    // Critical: Show immediately, bypass queue
                    DisplayNotificationRequested?.Invoke(notification);
                    break;

                case NotificationPriority.HIGH:
                    // High: Queue with animation
                    _highPriorityQueue.Enqueue(notification);
                    _ = ProcessHighPriorityQueueAsync();
                    break;

                case NotificationPriority.NORMAL:
                    // Normal: Standard notification, show standard toast
                    DisplayNotificationRequested?.Invoke(notification);
                    break;

                case NotificationPriority.SILENT:
                    // Silent: No UI, do nothing further visually
                    break;
            }
        }

        private async Task ProcessHighPriorityQueueAsync()
        {
            await _queueLock.WaitAsync();
            try
            {
                if (_isProcessingQueue) return;
                _isProcessingQueue = true;
            }
            finally
            {
                _queueLock.Release();
            }

            try
            {
                while (_highPriorityQueue.TryDequeue(out var nextNotification))
                {
                    DisplayNotificationRequested?.Invoke(nextNotification);
                    // Introduce artificial delay to let animation finish before showing next
                    await Task.Delay(4000); // 4 seconds auto-dismiss spacing
                }
            }
            finally
            {
                await _queueLock.WaitAsync();
                try
                {
                    _isProcessingQueue = false;
                }
                finally
                {
                    _queueLock.Release();
                }
            }
        }
    }
}
