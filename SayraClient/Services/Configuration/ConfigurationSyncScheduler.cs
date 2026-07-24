using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sayra.Client.Configuration.Synchronization;
using SayraClient.Services;

namespace SayraClient.Services.Configuration;

public class ConfigurationSyncScheduler : SupervisedBackgroundService
{
    private readonly IConfigurationSynchronizationService _syncService;
    private readonly IConfiguration _appConfiguration;
    private readonly Random _random = new();

    private TimeSpan _syncInterval = TimeSpan.FromMinutes(15);
    private int _maxRetryAttempts = 5;
    private TimeSpan _baseBackoff = TimeSpan.FromSeconds(10);
    private TimeSpan _maxBackoff = TimeSpan.FromMinutes(5);

    public ConfigurationSyncScheduler(
        ILogger<ConfigurationSyncScheduler> logger,
        IServiceHealthMonitor healthMonitor,
        IConfigurationSynchronizationService syncService,
        IConfiguration appConfiguration)
        : base(logger, healthMonitor, "ConfigurationSyncScheduler")
    {
        _syncService = syncService;
        _appConfiguration = appConfiguration;
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            // Try loading settings from app configuration (e.g., appsettings.json)
            var intervalMin = _appConfiguration.GetValue<double>("SyncSettings:IntervalMinutes", 15);
            _syncInterval = TimeSpan.FromMinutes(intervalMin);

            _maxRetryAttempts = _appConfiguration.GetValue<int>("SyncSettings:MaxRetryAttempts", 5);

            var baseBackoffSec = _appConfiguration.GetValue<double>("SyncSettings:BaseBackoffSeconds", 10);
            _baseBackoff = TimeSpan.FromSeconds(baseBackoffSec);

            var maxBackoffMin = _appConfiguration.GetValue<double>("SyncSettings:MaxBackoffMinutes", 5);
            _maxBackoff = TimeSpan.FromMinutes(maxBackoffMin);

            _logger.LogInformation($"Scheduler settings loaded: Interval={_syncInterval.TotalMinutes}m, MaxRetries={_maxRetryAttempts}, BaseBackoff={_baseBackoff.TotalSeconds}s.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load scheduler settings from configuration. Using default settings.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ConfigurationSyncScheduler background loop starting...");

        // 1. Randomized Startup Jitter to prevent concurrent server storming
        int jitterMs = _random.Next(2000, 15000); // 2 to 15 seconds randomized jitter
        _logger.LogInformation($"Applying startup jitter: delaying loop start by {jitterMs}ms.");

        try
        {
            await Task.Delay(jitterMs, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Startup jitter canceled. Shutting down.");
            return;
        }

        int retryCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Triggering scheduled configuration synchronization...");
            _healthMonitor.ReportState(_serviceName, ServiceHealthState.Healthy, $"Running sync. Active interval: {_syncInterval.TotalMinutes}m");

            bool syncSuccess = false;
            try
            {
                syncSuccess = await _syncService.PullAndApplyAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred during background configuration synchronization pull.");
            }

            if (syncSuccess)
            {
                _logger.LogInformation("Scheduled configuration synchronization completed successfully.");
                retryCount = 0; // Reset retry count on success
                _healthMonitor.ReportState(_serviceName, ServiceHealthState.Healthy, $"Last sync succeeded. Waiting {_syncInterval.TotalMinutes}m for next cycle.");

                try
                {
                    await Task.Delay(_syncInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            else
            {
                retryCount++;
                _logger.LogWarning($"Configuration synchronization failed. Attempt {retryCount} of {_maxRetryAttempts}.");

                if (retryCount >= _maxRetryAttempts)
                {
                    _logger.LogError($"Max synchronization retry attempts ({_maxRetryAttempts}) reached. Progressing with normal interval interval.");
                    _healthMonitor.ReportState(_serviceName, ServiceHealthState.Degraded, $"Failed {_maxRetryAttempts} consecutive times. Rescheduling in {_syncInterval.TotalMinutes}m.");
                    retryCount = 0; // Reset to try fresh next cycle

                    try
                    {
                        await Task.Delay(_syncInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                else
                {
                    // Exponential backoff calculation: baseBackoff * 2^(retryCount-1) + jitter
                    double multiplier = Math.Pow(2, retryCount - 1);
                    double backoffMs = _baseBackoff.TotalMilliseconds * multiplier;

                    // Add small randomized jitter (up to 15% of backoff time) to desynchronize retries
                    double maxJitter = backoffMs * 0.15;
                    double backoffJitter = _random.NextDouble() * maxJitter;
                    double finalBackoffMs = backoffMs + backoffJitter;

                    if (finalBackoffMs > _maxBackoff.TotalMilliseconds)
                    {
                        finalBackoffMs = _maxBackoff.TotalMilliseconds;
                    }

                    var backoffTimeSpan = TimeSpan.FromMilliseconds(finalBackoffMs);
                    _logger.LogWarning($"Backing off synchronization for {backoffTimeSpan.TotalSeconds:F1}s before retry attempt.");
                    _healthMonitor.ReportState(_serviceName, ServiceHealthState.Degraded, $"Sync failed. Backing off for {backoffTimeSpan.TotalSeconds:F1}s.");

                    try
                    {
                        await Task.Delay(backoffTimeSpan, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        _logger.LogInformation("ConfigurationSyncScheduler background loop gracefully terminated.");
    }
}
