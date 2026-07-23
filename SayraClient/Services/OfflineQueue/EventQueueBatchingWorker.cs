using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.OfflineQueue;
using Sayra.Client.OfflineQueue.Models;
using Sayra.Client.Diagnostics.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Services;

namespace SayraClient.Services.OfflineQueue
{
    public class EventQueueBatchingWorker : SupervisedBackgroundService
    {
        private readonly IAuditLogRepository _repository;
        private readonly LogBatchingManager _batchingManager;
        private readonly TcpClientManager _tcpClientManager;
        private readonly IOfflineQueueManager _offlineQueue;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly SemaphoreSlim _flushSignal = new(0, 1);
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);

        public EventQueueBatchingWorker(
            ILogger<EventQueueBatchingWorker> logger,
            IServiceHealthMonitor healthMonitor,
            IAuditLogRepository repository,
            LogBatchingManager batchingManager,
            TcpClientManager tcpClientManager,
            IOfflineQueueManager offlineQueue,
            IEventDispatcher eventDispatcher)
            : base(logger, healthMonitor, "EventQueueBatchingWorker")
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _batchingManager = batchingManager ?? throw new ArgumentNullException(nameof(batchingManager));
            _tcpClientManager = tcpClientManager ?? throw new ArgumentNullException(nameof(tcpClientManager));
            _offlineQueue = offlineQueue ?? throw new ArgumentNullException(nameof(offlineQueue));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));

            // Listen for critical events to trigger immediate flush
            _eventDispatcher.RegisterHandler<EventLogEntry>(OnEventLogDispatched);
        }

        private void OnEventLogDispatched(EventLogEntry entry)
        {
            if (entry.Severity == "FATAL" || entry.Category == "SECURITY")
            {
                _logger.LogInformation("Critical event detected. Signalling immediate flush.");
                TriggerFlush();
            }
        }

        public void TriggerFlush()
        {
            try
            {
                if (_flushSignal.CurrentCount == 0)
                {
                    _flushSignal.Release();
                }
            }
            catch
            {
                // Ignore potential semaphore release exceptions
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EventQueueBatchingWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during event batch processing cycle.");
                }

                try
                {
                    // Wait for either the interval or an immediate flush signal
                    await Task.WhenAny(
                        _flushSignal.WaitAsync(stoppingToken),
                        Task.Delay(_pollInterval, stoppingToken)
                    );
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("EventQueueBatchingWorker stopped.");
        }

        private async Task ProcessBatchAsync(CancellationToken stoppingToken)
        {
            var pendingCount = await _repository.GetPendingLogsCountAsync();
            if (pendingCount == 0) return;

            _logger.LogInformation("Processing batches for {Count} pending logs.", pendingCount);

            while (pendingCount > 0 && !stoppingToken.IsCancellationRequested)
            {
                var logs = await _repository.GetPendingLogsAsync(limit: 100);
                if (logs.Count == 0) break;

                // Generate GZipped compressed batch payload
                var compressedBytes = _batchingManager.CreateCompressedBatch(logs);
                var base64Payload = Convert.ToBase64String(compressedBytes);

                bool delivered = false;

                if (_tcpClientManager.IsConnected)
                {
                    // Try sending directly to the server
                    var batchEvent = new ClientEvent
                    {
                        EventType = "LOG_BATCH",
                        Priority = QueuePriority.NORMAL,
                        Payload = base64Payload
                    };

                    try
                    {
                        delivered = await _tcpClientManager.SendMessageAsync(batchEvent, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send log batch to server over TCP.");
                    }
                }

                if (delivered)
                {
                    _logger.LogInformation("Successfully uploaded batch of {Count} logs directly to server.", logs.Count);
                    await _repository.DeleteLogsAsync(logs.Select(l => l.EventId).ToList());
                }
                else
                {
                    // Redirect batch to offline queue
                    _logger.LogInformation("TCP disconnected or failed. Redirecting batch of {Count} logs to local encrypted offline queue.", logs.Count);

                    var batchEvent = new ClientEvent
                    {
                        EventType = "LOG_BATCH",
                        Priority = QueuePriority.NORMAL,
                        Payload = base64Payload
                    };

                    await _offlineQueue.AddEventAsync(batchEvent);
                    await _repository.DeleteLogsAsync(logs.Select(l => l.EventId).ToList());
                }

                pendingCount = await _repository.GetPendingLogsCountAsync();
            }
        }
    }
}
