using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Exceptions;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Telemetry.Historical
{
    /// <summary>
    /// Thread-safe enterprise orchestrator for storing, querying, archiving, and cleaning up historical metrics.
    /// Supports dynamic linear regression trend analysis for capacity forecasting.
    /// </summary>
    public class HistoricalMetricsService : IHistoricalMetricsService
    {
        private readonly IHistoricalStorageProvider _storageProvider;
        private readonly IHistoricalMetricRepository _metricRepository;
        private readonly IMetricSeriesRepository _seriesRepository;
        private readonly IPerformanceSnapshotRepository _performanceRepository;
        private readonly IAuditMetricRepository _auditRepository;
        private readonly IHistoricalArchiveProvider _archiveProvider;
        private readonly HistoricalStorageOptions _storageOptions;
        private readonly RetentionOptions _retentionOptions;
        private readonly ILogger<HistoricalMetricsService> _logger;
        private readonly SemaphoreSlim _cleanupLock = new(1, 1);

        public HistoricalMetricsService(
            IHistoricalStorageProvider storageProvider,
            IHistoricalMetricRepository metricRepository,
            IMetricSeriesRepository seriesRepository,
            IPerformanceSnapshotRepository performanceRepository,
            IAuditMetricRepository auditRepository,
            IHistoricalArchiveProvider archiveProvider,
            IOptions<HistoricalStorageOptions> storageOptions,
            IOptions<RetentionOptions> retentionOptions,
            ILogger<HistoricalMetricsService> logger)
        {
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _metricRepository = metricRepository ?? throw new ArgumentNullException(nameof(metricRepository));
            _seriesRepository = seriesRepository ?? throw new ArgumentNullException(nameof(seriesRepository));
            _performanceRepository = performanceRepository ?? throw new ArgumentNullException(nameof(performanceRepository));
            _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
            _archiveProvider = archiveProvider ?? throw new ArgumentNullException(nameof(archiveProvider));
            _storageOptions = storageOptions?.Value ?? throw new ArgumentNullException(nameof(storageOptions));
            _retentionOptions = retentionOptions?.Value ?? throw new ArgumentNullException(nameof(retentionOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SaveHistoricalMetricAsync(HistoricalMetric metric, CancellationToken cancellationToken = default)
        {
            if (metric == null) throw new ArgumentNullException(nameof(metric));

            _logger.LogDebug("Saving historical metric: {Name} ({Value})", metric.MetricName, metric.AverageValue);
            try
            {
                await _metricRepository.InsertAsync(metric, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save historical metric {Name}.", metric.MetricName);
                throw new HistoricalStorageException($"Failed to save historical metric.", ex);
            }
        }

        public async Task<IReadOnlyCollection<HistoricalMetric>> GetHistoricalMetricsAsync(string name, DateTime start, DateTime end, CollectionInterval interval, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

            _logger.LogDebug("Querying historical metrics for {Name} between {Start} and {End}", name, start, end);
            try
            {
                return await _metricRepository.QueryAsync(name, start, end, interval, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve historical metrics for {Name}.", name);
                throw new HistoricalStorageException($"Failed to retrieve historical metrics.", ex);
            }
        }

        public async Task<CapacityForecast> ForecastCapacityAsync(string name, int projectionDays, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));
            if (projectionDays <= 0) throw new ArgumentOutOfRangeException(nameof(projectionDays), "Projection days must be greater than zero.");

            _logger.LogInformation("Generating capacity forecast for metric '{Name}' with a {Days}-day projection horizon.", name, projectionDays);

            try
            {
                var end = DateTime.UtcNow;
                var start = end.AddDays(-30); // Analyze the past 30 days of data

                // Try Hourly first, then fallback to Daily or any available
                var historical = await _metricRepository.QueryAsync(name, start, end, null, cancellationToken);
                if (historical == null || historical.Count < 2)
                {
                    _logger.LogWarning("Insufficient historical data points (found {Count}) to compute capacity forecast for '{Name}'.", historical?.Count ?? 0, name);
                    return new CapacityForecast
                    {
                        MetricName = name,
                        CurrentUsage = historical?.FirstOrDefault()?.AverageValue ?? 0.0,
                        ForecastedUsage = historical?.FirstOrDefault()?.AverageValue ?? 0.0,
                        ForecastHorizon = DateTime.UtcNow.AddDays(projectionDays),
                        ConfidenceLevel = 0.0,
                        Recommendation = "Insufficient historical data to calculate trend."
                    };
                }

                // Implement High-Precision Linear Regression: y = mx + c
                var points = historical.OrderBy(m => m.Timestamp).ToList();
                double firstTimestampTicks = points[0].Timestamp.Ticks;
                double ticksPerDay = TimeSpan.TicksPerDay;

                double sumX = 0;
                double sumY = 0;
                double sumXY = 0;
                double sumXX = 0;
                int n = points.Count;

                foreach (var m in points)
                {
                    double x = (m.Timestamp.Ticks - firstTimestampTicks) / ticksPerDay;
                    double y = m.AverageValue;

                    sumX += x;
                    sumY += y;
                    sumXY += x * y;
                    sumXX += x * x;
                }

                double denominator = (n * sumXX) - (sumX * sumX);
                double slope = 0;
                double intercept = points[0].AverageValue;

                if (Math.Abs(denominator) > 1e-9)
                {
                    slope = ((n * sumXY) - (sumX * sumY)) / denominator;
                    intercept = ((sumXX * sumY) - (sumX * sumXY)) / denominator;
                }

                double currentUsage = points.Last().AverageValue;
                double daysToProject = (DateTime.UtcNow.AddDays(projectionDays).Ticks - firstTimestampTicks) / ticksPerDay;
                double forecastedUsage = (slope * daysToProject) + intercept;
                if (forecastedUsage < 0) forecastedUsage = 0; // Usage cannot be negative

                // Calculate a simple confidence level based on variance or standard default
                double confidence = 0.85;
                string recommendation = "No Action Required";

                if (slope > 0)
                {
                    var growthRate = (slope / currentUsage) * 100;
                    if (growthRate > 5.0)
                    {
                        recommendation = $"Resource '{name}' is growing rapidly at {growthRate:F1}% per day. Upgrading capacity or purging logs is highly recommended.";
                    }
                    else
                    {
                        recommendation = $"Resource usage is steadily increasing. Monitor closely.";
                    }
                }
                else if (slope < 0)
                {
                    recommendation = "Usage trend is declining. Underutilized resources can be optimized.";
                }

                return new CapacityForecast
                {
                    MetricName = name,
                    CurrentUsage = currentUsage,
                    ForecastedUsage = forecastedUsage,
                    ForecastHorizon = DateTime.UtcNow.AddDays(projectionDays),
                    ConfidenceLevel = confidence,
                    Recommendation = recommendation
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate capacity forecast for metric {Name}.", name);
                throw new HistoricalStorageException($"Capacity forecasting failed.", ex);
            }
        }

        /// <summary>
        /// Executes the complete retention policy: archives expired telemetry data and prunes tables.
        /// Also enforces database storage ceiling size limits dynamically.
        /// </summary>
        public async Task ExecuteRetentionPoliciesAsync(CancellationToken cancellationToken = default)
        {
            // Non-blocking write serialization for cleanup
            var acquired = await _cleanupLock.WaitAsync(0, cancellationToken);
            if (!acquired)
            {
                _logger.LogWarning("Retention policies cleanup is already running in another task. Skipping.");
                return;
            }

            try
            {
                _logger.LogInformation("Executing retention policy: Type={Policy}, Days={Days}", _retentionOptions.PolicyType, _retentionOptions.RetentionDays);

                var cutoffUtc = CalculateCutoffDate();

                // 1. Fetch expired records for archiving
                var expiredMetrics = await _metricRepository.GetExpiredAsync(cutoffUtc, cancellationToken);
                if (expiredMetrics.Count > 0)
                {
                    _logger.LogInformation("Found {Count} expired historical metrics eligible for archiving.", expiredMetrics.Count);

                    var archiveDir = _storageOptions.ArchiveDirectory;
                    if (string.IsNullOrWhiteSpace(archiveDir))
                    {
                        archiveDir = Path.Combine(AppContext.BaseDirectory, "Data", "Archive");
                    }

                    if (!Directory.Exists(archiveDir))
                    {
                        Directory.CreateDirectory(archiveDir);
                    }

                    var archiveFile = Path.Combine(archiveDir, $"historical_metrics_archive_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

                    // Pluggable archiving
                    await _archiveProvider.ArchiveAsync(archiveFile, expiredMetrics, cancellationToken);

                    // Optional: validate the archive before deleting from the database
                    bool isValid = await _archiveProvider.ValidateArchiveAsync(archiveFile, cancellationToken);
                    if (!isValid)
                    {
                        throw new HistoricalStorageException("Archive integrity validation failed. Postponing database pruning.");
                    }

                    _logger.LogInformation("Archive validated. Pruning expired historical metrics database tables.");
                }

                // 2. Delete expired records across all repositories
                await _metricRepository.DeleteAsync(cutoffUtc, cancellationToken);
                await _seriesRepository.DeleteAsync(cutoffUtc, cancellationToken);
                await _performanceRepository.DeleteAsync(cutoffUtc, cancellationToken);
                await _auditRepository.DeleteAsync(cutoffUtc, cancellationToken);

                // 3. Enforce maximum storage size limit
                await EnforceStorageSizeLimitAsync(cancellationToken);

                _logger.LogInformation("Retention policy execution completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing retention policies.");
                throw new HistoricalStorageException("Retention policy execution failed.", ex);
            }
            finally
            {
                _cleanupLock.Release();
            }
        }

        private DateTime CalculateCutoffDate()
        {
            int days = _retentionOptions.RetentionDays;
            if (days <= 0) days = 30;

            if (_storageOptions.CustomRetentionHours.HasValue)
            {
                _logger.LogInformation("Using custom retention window of {Hours} hours.", _storageOptions.CustomRetentionHours.Value);
                return DateTime.UtcNow.AddHours(-_storageOptions.CustomRetentionHours.Value);
            }

            return _retentionOptions.PolicyType switch
            {
                RetentionPolicyType.Hourly => DateTime.UtcNow.AddHours(-days),
                RetentionPolicyType.Daily => DateTime.UtcNow.AddDays(-days),
                RetentionPolicyType.Weekly => DateTime.UtcNow.AddDays(-days * 7),
                RetentionPolicyType.Monthly => DateTime.UtcNow.AddDays(-days * 30),
                _ => DateTime.UtcNow.AddDays(-days)
            };
        }

        private async Task EnforceStorageSizeLimitAsync(CancellationToken cancellationToken)
        {
            var maxBytes = _storageOptions.MaxStorageSizeBytes;
            if (maxBytes <= 0) return; // Ceiling not configured

            var currentSize = _storageProvider.GetStorageSizeBytes();
            if (currentSize <= maxBytes) return;

            _logger.LogWarning("Historical database file size ({CurrentSize} bytes) exceeds ceiling limit ({MaxLimit} bytes). Enforcing emergency pruning.", currentSize, maxBytes);

            // Dynamically prune oldest data in steps of 5 days further until under size limit (or max 10 steps to prevent infinite loop)
            int steps = 0;
            var currentCutoff = CalculateCutoffDate();

            while (currentSize > maxBytes && steps < 10)
            {
                steps++;
                currentCutoff = currentCutoff.AddDays(5);
                _logger.LogInformation("Emergency pruning: Deleting historical records older than {Cutoff}", currentCutoff);

                await _metricRepository.DeleteAsync(currentCutoff, cancellationToken);
                await _seriesRepository.DeleteAsync(currentCutoff, cancellationToken);
                await _performanceRepository.DeleteAsync(currentCutoff, cancellationToken);
                await _auditRepository.DeleteAsync(currentCutoff, cancellationToken);

                currentSize = _storageProvider.GetStorageSizeBytes();
            }

            _logger.LogInformation("Emergency database sizing completed in {Steps} pruning iteration(s). New file size: {CurrentSize} bytes.", steps, currentSize);
        }
    }
}
