using System;
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
    /// Monitors the update subsystem's physical health, engine readiness, storage headroom, and database integrity.
    /// </summary>
    public class HealthMonitor : IHealthMonitor
    {
        private readonly IStorageQuotaManager _storageQuotaManager;
        private readonly IUpdateHistoryRepository _historyRepository;
        private readonly IDownloadManager _downloadManager;
        private readonly IInstallerEngine _installerEngine;
        private readonly IRollbackEngine _rollbackEngine;
        private readonly MonitoringOptions _monitoringOptions;
        private readonly ILogger<HealthMonitor> _logger;

        public HealthMonitor(
            IStorageQuotaManager storageQuotaManager,
            IUpdateHistoryRepository historyRepository,
            IDownloadManager downloadManager,
            IInstallerEngine installerEngine,
            IRollbackEngine rollbackEngine,
            IOptions<MonitoringOptions> monitoringOptions,
            ILogger<HealthMonitor> logger)
        {
            _storageQuotaManager = storageQuotaManager ?? throw new ArgumentNullException(nameof(storageQuotaManager));
            _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
            _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
            _installerEngine = installerEngine ?? throw new ArgumentNullException(nameof(installerEngine));
            _rollbackEngine = rollbackEngine ?? throw new ArgumentNullException(nameof(rollbackEngine));
            _monitoringOptions = monitoringOptions.Value;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthMetric> EvaluateHealthAsync(CancellationToken cancellationToken = default)
        {
            if (!_monitoringOptions.Enabled)
            {
                return new HealthMetric
                {
                    ComponentName = "UpdateSubsystem",
                    IsHealthy = true,
                    LastErrorMessage = "Health monitoring is disabled.",
                    CheckedAtUtc = DateTime.UtcNow
                };
            }

            bool dbHealthy = true;
            bool storageHealthy = true;
            bool enginesHealthy = true;
            string lastErrorMessage = string.Empty;

            // 1. Verify Database Connection Integrity
            try
            {
                await _historyRepository.GetLatestAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                dbHealthy = false;
                lastErrorMessage += $"[Database Error: {ex.Message}] ";
                _logger.LogError(ex, "Health check: Database integrity verification failed.");
            }

            // 2. Verify Storage Space Room
            try
            {
                var stats = await _storageQuotaManager.GetStatisticsAsync(cancellationToken);
                if (stats.AvailableFreeSpaceBytes < _monitoringOptions.MinStorageBytes)
                {
                    storageHealthy = false;
                    lastErrorMessage += $"[Insufficient Storage: Free {stats.AvailableFreeSpaceBytes} bytes < Min Required {_monitoringOptions.MinStorageBytes} bytes] ";
                    _logger.LogWarning("Health check: Low storage capacity detected on the target drive.");
                }
            }
            catch (Exception ex)
            {
                storageHealthy = false;
                lastErrorMessage += $"[Storage Retrieval Error: {ex.Message}] ";
                _logger.LogError(ex, "Health check: Storage statistics query failed.");
            }

            // 3. Verify Engines are loaded
            if (_downloadManager == null || _installerEngine == null || _rollbackEngine == null)
            {
                enginesHealthy = false;
                lastErrorMessage += "[Core Engines Missing from Dependency Graph] ";
            }

            bool overallHealthy = dbHealthy && storageHealthy && enginesHealthy;

            var result = new HealthMetric
            {
                ComponentName = "UpdateSubsystem",
                IsHealthy = overallHealthy,
                LastErrorMessage = overallHealthy ? "System healthy." : lastErrorMessage.Trim(),
                CheckedAtUtc = DateTime.UtcNow,
                CustomMetricsData = $"{{ \"DatabaseHealthy\": {dbHealthy.ToString().ToLower()}, \"StorageHealthy\": {storageHealthy.ToString().ToLower()}, \"EnginesHealthy\": {enginesHealthy.ToString().ToLower()} }}"
            };

            return result;
        }

        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            var metric = await EvaluateHealthAsync(cancellationToken);
            return metric.IsHealthy;
        }

        public async Task<string> GetLastSuccessfulUpdateVersionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var latest = await _historyRepository.GetLatestAsync(cancellationToken);
                if (latest != null && string.Equals(latest.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    return latest.Version;
                }
                return "0.0.0";
            }
            catch (Exception ex)
            {
                throw new HealthMonitoringException("Failed to query the last successful update version.", ex);
            }
        }

        public async Task<DateTime?> GetLastSuccessfulUpdateUtcAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var latest = await _historyRepository.GetLatestAsync(cancellationToken);
                if (latest != null && string.Equals(latest.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    return latest.CompletionTime ?? latest.InstallationTime;
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new HealthMonitoringException("Failed to query the last successful update timestamp.", ex);
            }
        }
    }
}
