using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.OfflineQueue;
using Sayra.Client.OfflineQueue.Models;
using Sayra.Client.OfflineQueue.Security;
using Sayra.Client.OfflineQueue.Serialization;

namespace SayraClient.Services.OfflineQueue;

public class QueueProcessorWorker : SupervisedBackgroundService
{
    private readonly IOfflineQueueManager _queueManager;
    private readonly IEventSerializer _serializer;
    private readonly IQueueSecurityManager _securityManager;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    private int _maxRetries = 5;

    // Simulated network connection state for offline queue tests
    public static bool SimulateNetworkFailure { get; set; } = false;

    public QueueProcessorWorker(
        ILogger<QueueProcessorWorker> logger,
        IServiceHealthMonitor healthMonitor,
        IOfflineQueueManager queueManager,
        IEventSerializer serializer,
        IQueueSecurityManager securityManager)
        : base(logger, healthMonitor, "QueueProcessorWorker")
    {
        _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _securityManager = securityManager ?? throw new ArgumentNullException(nameof(securityManager));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QueueProcessorWorker execution loop started. Poll interval: {Interval}s, Max Retries: {Max}.", _pollInterval.TotalSeconds, _maxRetries);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pendingEvents = await _queueManager.GetPendingEventsAsync(limit: 10);
                if (pendingEvents.Count > 0)
                {
                    _logger.LogInformation("Retrieved {Count} pending/failed events from offline queue for processing.", pendingEvents.Count);

                    foreach (var item in pendingEvents)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        await ProcessQueueItemAsync(item);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during offline queue processing cycle.");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("QueueProcessorWorker execution loop stopped.");
    }

    private async Task ProcessQueueItemAsync(QueueItem item)
    {
        try
        {
            _logger.LogInformation("Processing Queue Item {Id} [Type: '{Type}', Attempt: {Attempt}]", item.Id, item.EventType, item.RetryCount + 1);

            // Decrypt the payload
            string decryptedPayloadJson = _securityManager.DecryptPayload(item.Payload);
            var clientEvent = _serializer.Deserialize(decryptedPayloadJson);

            // Simulate Delivery / Dispatch
            bool success = await DispatchEventToServerAsync(clientEvent);

            if (success)
            {
                _logger.LogInformation("Successfully delivered Event {EventId} (Queue Item {Id}) to the server.", clientEvent.EventId, item.Id);
                await _queueManager.MarkCompletedAsync(item.Id);
            }
            else
            {
                var errorMsg = "Simulated network transmission timeout.";
                _logger.LogWarning("Failed to deliver Event {EventId} (Queue Item {Id}). Error: {Error}", clientEvent.EventId, item.Id, errorMsg);
                await _queueManager.RecordFailureAsync(item.Id, errorMsg, _maxRetries);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt or deserialize event for Queue Item {Id}.", item.Id);
            await _queueManager.RecordFailureAsync(item.Id, $"Deser/Decrypt error: {ex.Message}", _maxRetries);
        }
    }

    private async Task<bool> DispatchEventToServerAsync(ClientEvent evt)
    {
        // Simulate a tiny network latency
        await Task.Delay(100);

        if (SimulateNetworkFailure)
        {
            return false;
        }

        // Standard behavior is success unless SimulateNetworkFailure is set
        return true;
    }
}
