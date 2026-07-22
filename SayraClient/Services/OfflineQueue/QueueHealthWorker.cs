using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.OfflineQueue;

namespace SayraClient.Services.OfflineQueue;

public class QueueHealthWorker : SupervisedBackgroundService
{
    private readonly IOfflineQueueManager _queueManager;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(15);
    private readonly long _maxDatabaseSizeBytes = 50 * 1024 * 1024; // 50 MB threshold
    private readonly int _highPendingCountThreshold = 1000;

    public QueueHealthWorker(
        ILogger<QueueHealthWorker> logger,
        IServiceHealthMonitor healthMonitor,
        IOfflineQueueManager queueManager)
        : base(logger, healthMonitor, "QueueHealthWorker")
    {
        _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QueueHealthWorker started. Check interval: {Interval}s.", _checkInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunHealthCheckCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during queue health check cycle.");
                _healthMonitor.ReportFailure("QueueHealthWorker", ex, "Health check cycle crashed.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("QueueHealthWorker stopped.");
    }

    private async Task RunHealthCheckCycleAsync(CancellationToken ct)
    {
        _logger.LogDebug("Running Offline Queue health check cycle...");

        // 1. Verify SQLite database integrity
        bool isIntegrityOk = await _queueManager.VerifyIntegrityAsync();
        if (!isIntegrityOk)
        {
            _logger.LogError("Database corruption detected by integrity check! Forcing recreation...");
            _healthMonitor.ReportState("QueueHealthWorker", ServiceHealthState.Failed, "Database corruption detected.");

            // Recreate the database to recover from corruption
            await _queueManager.ForceRecreateDatabaseAsync();

            _healthMonitor.ReportState("QueueHealthWorker", ServiceHealthState.Recovering, "Database recreated. Recovering...");
            return;
        }

        // 2. Monitor database file size
        long dbSizeBytes = await _queueManager.GetQueueSizeInBytesAsync();
        double dbSizeMb = dbSizeBytes / (1024.0 * 1024.0);
        _logger.LogDebug("Offline Queue database file size: {SizeMb:F2} MB", dbSizeMb);

        if (dbSizeBytes > _maxDatabaseSizeBytes)
        {
            _logger.LogWarning("Database size ({SizeMb:F2} MB) exceeds maximum threshold ({MaxMb} MB). Active pruning might be required.", dbSizeMb, _maxDatabaseSizeBytes / (1024.0 * 1024.0));
        }

        // 3. Monitor pending queue item count
        int pendingCount = await _queueManager.GetPendingCountAsync();
        _logger.LogDebug("Offline Queue pending count: {Count} items.", pendingCount);

        // 4. Prune completed events older than 7 days
        await _queueManager.DeleteExpiredEventsAsync(TimeSpan.FromDays(7));

        // 5. Update health state in ServiceHealthMonitor
        if (pendingCount > _highPendingCountThreshold)
        {
            _healthMonitor.ReportState("QueueHealthWorker", ServiceHealthState.Degraded, $"High pending event load: {pendingCount} events.");
        }
        else
        {
            _healthMonitor.ReportState("QueueHealthWorker", ServiceHealthState.Healthy, $"Healthy. Size: {dbSizeMb:F2} MB, Pending: {pendingCount}");
        }
    }
}
