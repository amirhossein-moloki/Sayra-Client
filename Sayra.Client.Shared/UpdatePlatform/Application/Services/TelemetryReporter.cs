using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Implements non-blocking, asynchronous telemetry capture, enrichment, local buffering, and background delivery.
    /// </summary>
    public class TelemetryReporter : ITelemetryReporter, IDisposable
    {
        private readonly ITelemetryOfflineQueue _offlineQueue;
        private readonly IAdminIntegrationClient _adminClient;
        private readonly ILogger<TelemetryReporter> _logger;
        private readonly TelemetryOptions _telemetryOptions;
        private readonly ReportingOptions _reportingOptions;
        private readonly string _deviceIdentifier;

        private readonly SemaphoreSlim _flushLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _backgroundProcessorTask;
        private readonly AutoResetEvent _flushSignal = new(false);

        public TelemetryReporter(
            ITelemetryOfflineQueue offlineQueue,
            IAdminIntegrationClient adminClient,
            ILogger<TelemetryReporter> logger,
            IOptions<TelemetryOptions> telemetryOptions,
            IOptions<ReportingOptions> reportingOptions)
        {
            _offlineQueue = offlineQueue ?? throw new ArgumentNullException(nameof(offlineQueue));
            _adminClient = adminClient ?? throw new ArgumentNullException(nameof(adminClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryOptions = telemetryOptions.Value;
            _reportingOptions = reportingOptions.Value;
            _deviceIdentifier = Environment.MachineName;

            // Start background worker for periodic processing and retry handling
            if (_telemetryOptions.Enabled)
            {
                _backgroundProcessorTask = Task.Run(BackgroundProcessingLoopAsync);
            }
            else
            {
                _backgroundProcessorTask = Task.CompletedTask;
            }
        }

        public async Task RecordEventAsync(
            string eventType,
            string correlationId,
            string sourceVersion,
            string targetVersion,
            bool success,
            string errorCode = "",
            string errorMessage = "",
            string payloadJson = "",
            CancellationToken cancellationToken = default)
        {
            if (!_telemetryOptions.Enabled) return;

            if (string.IsNullOrWhiteSpace(eventType))
            {
                throw new TelemetryException("Telemetry EventType cannot be null or empty.");
            }

            // Enforce Non-blocking guarantee: execute validation and queuing in a separate Task context
            _ = Task.Run(async () =>
            {
                try
                {
                    var telemetryEvent = new UpdateTelemetryEvent
                    {
                        EventId = Guid.NewGuid(),
                        EventType = eventType,
                        TimestampUtc = DateTime.UtcNow,
                        CorrelationId = correlationId ?? string.Empty,
                        SourceVersion = sourceVersion ?? string.Empty,
                        TargetVersion = targetVersion ?? string.Empty,
                        Success = success,
                        ErrorCode = errorCode ?? string.Empty,
                        ErrorMessage = errorMessage ?? string.Empty,
                        DeviceIdentifier = _deviceIdentifier,
                        PayloadJson = payloadJson ?? string.Empty
                    };

                    _logger.LogInformation("Enqueuing update telemetry event {EventType} (Correlation ID: {CorrelationId})", eventType, correlationId);

                    await _offlineQueue.EnqueueAsync(telemetryEvent, _cts.Token);
                    _flushSignal.Set(); // Trigger immediate processing
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to capture telemetry event {EventType} non-blockingly.", eventType);
                }
            }, CancellationToken.None);

            await Task.CompletedTask;
        }

        public async Task RecordMetricAsync(UpdateOperationMetric metric, CancellationToken cancellationToken = default)
        {
            if (metric == null) throw new ArgumentNullException(nameof(metric));

            string payload = JsonSerializer.Serialize(metric);
            await RecordEventAsync(
                "UpdateOperationMetric",
                Guid.NewGuid().ToString(),
                string.Empty,
                string.Empty,
                metric.Success,
                metric.ErrorCode,
                metric.Details,
                payload,
                cancellationToken);
        }

        public async Task RecordMetricAsync(DownloadMetric metric, CancellationToken cancellationToken = default)
        {
            if (metric == null) throw new ArgumentNullException(nameof(metric));

            string payload = JsonSerializer.Serialize(metric);
            await RecordEventAsync(
                "DownloadMetric",
                metric.PackageId.ToString(),
                string.Empty,
                string.Empty,
                metric.Success,
                metric.ErrorCode,
                $"Download duration: {metric.Duration.TotalSeconds}s",
                payload,
                cancellationToken);
        }

        public async Task RecordMetricAsync(InstallationMetric metric, CancellationToken cancellationToken = default)
        {
            if (metric == null) throw new ArgumentNullException(nameof(metric));

            string payload = JsonSerializer.Serialize(metric);
            await RecordEventAsync(
                "InstallationMetric",
                Guid.NewGuid().ToString(),
                string.Empty,
                metric.TargetVersion,
                metric.Success,
                metric.ErrorCode,
                $"Files replaced: {metric.FilesReplacedCount}",
                payload,
                cancellationToken);
        }

        public async Task RecordMetricAsync(RollbackMetric metric, CancellationToken cancellationToken = default)
        {
            if (metric == null) throw new ArgumentNullException(nameof(metric));

            string payload = JsonSerializer.Serialize(metric);
            await RecordEventAsync(
                "RollbackMetric",
                Guid.NewGuid().ToString(),
                metric.FailedVersion,
                metric.RestoredVersion,
                metric.Success,
                string.Empty,
                metric.FailureReason,
                payload,
                cancellationToken);
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            _flushSignal.Set();
            await _flushLock.WaitAsync(cancellationToken);
            try
            {
                await ProcessPendingEventsInternalAsync(cancellationToken);
            }
            finally
            {
                _flushLock.Release();
            }
        }

        private async Task BackgroundProcessingLoopAsync()
        {
            var delay = TimeSpan.FromSeconds(_telemetryOptions.ReportingIntervalSeconds);
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    // Wait for either the periodic interval signal or an immediate flush trigger
                    await Task.Run(() => WaitHandle.WaitAny(new WaitHandle[] { _flushSignal, _cts.Token.WaitHandle }, delay));

                    if (_cts.IsCancellationRequested) break;

                    await ProcessPendingEventsAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception in telemetry background processing loop.");
                }
            }
        }

        private async Task ProcessPendingEventsAsync(CancellationToken cancellationToken)
        {
            if (!await _flushLock.WaitAsync(0, cancellationToken))
            {
                return; // Already flushing
            }

            try
            {
                await ProcessPendingEventsInternalAsync(cancellationToken);
            }
            finally
            {
                _flushLock.Release();
            }
        }

        private async Task ProcessPendingEventsInternalAsync(CancellationToken cancellationToken)
        {
            int count = await _offlineQueue.GetCountAsync(cancellationToken);
            while (count > 0 && !cancellationToken.IsCancellationRequested)
            {
                var events = await _offlineQueue.DequeueBatchAsync(20, cancellationToken);
                var processedIds = new List<Guid>();
                var failedIds = new List<Guid>();

                foreach (var ev in events)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    // Retrieve retry attempt details if we want, or keep it inside the sender logic
                    bool success = await TransmitWithRetryAsync(ev, cancellationToken);
                    if (success)
                    {
                        processedIds.Add(ev.EventId);
                    }
                    else
                    {
                        failedIds.Add(ev.EventId);
                        // Stop batch processing immediately upon transmission failure to respect offline states
                        break;
                    }
                }

                if (processedIds.Count > 0)
                {
                    await _offlineQueue.DeleteBatchAsync(processedIds, cancellationToken);
                }

                if (failedIds.Count > 0)
                {
                    await _offlineQueue.IncrementAttemptCountAsync(failedIds, cancellationToken);
                    break; // Halt batch processing since destination is unreachable
                }

                count = await _offlineQueue.GetCountAsync(cancellationToken);
            }
        }

        private async Task<bool> TransmitWithRetryAsync(UpdateTelemetryEvent ev, CancellationToken cancellationToken)
        {
            int attempt = 0;
            int maxAttempts = _reportingOptions.MaxRetryAttempts;
            int baseDelaySec = _reportingOptions.BaseDelaySeconds;

            while (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    bool success = await _adminClient.ReportTelemetryEventAsync(ev, cancellationToken);
                    if (success)
                    {
                        _logger.LogInformation("Successfully reported telemetry event {EventType} ({EventId})", ev.EventType, ev.EventId);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to transmit telemetry event {EventType}. Attempt {Attempt} of {MaxAttempts}", ev.EventType, attempt + 1, maxAttempts);
                }

                attempt++;
                if (attempt < maxAttempts)
                {
                    // Exponential backoff with random jitter
                    double delaySec = Math.Pow(2, attempt) * baseDelaySec;
                    var random = new Random();
                    double jitter = random.NextDouble() * 2.0; // up to 2 seconds jitter
                    var finalDelay = TimeSpan.FromSeconds(delaySec + jitter);

                    try
                    {
                        await Task.Delay(finalDelay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            return false;
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _backgroundProcessorTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch { }
            _cts.Dispose();
            _flushLock.Dispose();
            _flushSignal.Dispose();
        }
    }
}
