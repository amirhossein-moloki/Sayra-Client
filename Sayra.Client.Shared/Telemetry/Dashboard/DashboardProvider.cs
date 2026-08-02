using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Runtime.Application.Interfaces;

namespace Sayra.Client.Shared.Telemetry.Dashboard
{
    /// <summary>
    /// Thread-safe, non-blocking, asynchronous Dashboard Provider designed to serve local and remote monitor consumers.
    /// Aggregates data from existing services with robust failure isolation.
    /// </summary>
    public class DashboardProvider : IDashboardProvider
    {
        private readonly ILiveTelemetryService _liveTelemetry;
        private readonly IPerformanceMonitor _perfMonitor;
        private readonly IAlertEngine _alertEngine;
        private readonly IHealthMonitor _healthMonitor;
        private readonly ISessionRepository _sessionRepo;
        private readonly ISecurityHardeningService _securityHardening;
        private readonly IOptions<DashboardOptions> _options;
        private readonly ILogger<DashboardProvider> _logger;

        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private DateTime _lastRefreshTime = DateTime.MinValue;

        // Cached snapshot and read models
        private DashboardSnapshot _cachedSnapshot = new();
        private DashboardOverviewReadModel _cachedOverview = new();
        private DashboardSubsystemStatusReadModel _cachedSubsystems = new();
        private DashboardPerformanceSummaryReadModel _cachedPerformance = new();
        private DashboardAlertSummaryReadModel _cachedAlerts = new();
        private DashboardSecuritySummaryReadModel _cachedSecurity = new();
        private DashboardRecoverySummaryReadModel _cachedRecovery = new();
        private DashboardComplianceSummaryReadModel _cachedCompliance = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardProvider"/> class.
        /// </summary>
        public DashboardProvider(
            ILiveTelemetryService liveTelemetry,
            IPerformanceMonitor perfMonitor,
            IAlertEngine alertEngine,
            IHealthMonitor healthMonitor,
            ISessionRepository sessionRepo,
            ISecurityHardeningService securityHardening,
            IOptions<DashboardOptions> options,
            ILogger<DashboardProvider> logger)
        {
            _liveTelemetry = liveTelemetry;
            _perfMonitor = perfMonitor;
            _alertEngine = alertEngine;
            _healthMonitor = healthMonitor;
            _sessionRepo = sessionRepo;
            _securityHardening = securityHardening;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<DashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
        {
            await EnsureCacheValidAsync(cancellationToken).ConfigureAwait(false);
            return _cachedSnapshot;
        }

        /// <inheritdoc />
        public async Task StreamDashboardUpdatesAsync(Action<DashboardSnapshot> onUpdate, CancellationToken cancellationToken = default)
        {
            if (onUpdate == null) throw new ArgumentNullException(nameof(onUpdate));

            _logger.LogInformation("Dashboard stream subscription started.");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var snapshot = await GetDashboardSnapshotAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        onUpdate(snapshot);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing dashboard stream callback update action.");
                    }

                    int intervalSeconds = _options.Value.RefreshIntervalSeconds;
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Dashboard stream subscription cancelled.");
            }
        }

        /// <inheritdoc />
        public async Task<DashboardOverviewReadModel> GetOverviewAsync(CancellationToken cancellationToken = default)
        {
            await EnsureCacheValidAsync(cancellationToken).ConfigureAwait(false);
            return _cachedOverview;
        }

        /// <inheritdoc />
        public async Task<DashboardSubsystemStatusReadModel> GetSubsystemStatusAsync(CancellationToken cancellationToken = default)
        {
            await EnsureCacheValidAsync(cancellationToken).ConfigureAwait(false);
            return _cachedSubsystems;
        }

        /// <inheritdoc />
        public async Task<DashboardPerformanceSummaryReadModel> GetPerformanceSummaryAsync(CancellationToken cancellationToken = default)
        {
            await EnsureCacheValidAsync(cancellationToken).ConfigureAwait(false);
            return _cachedPerformance;
        }

        /// <inheritdoc />
        public async Task<DashboardAlertSummaryReadModel> GetAlertSummaryAsync(CancellationToken cancellationToken = default)
        {
            await EnsureCacheValidAsync(cancellationToken).ConfigureAwait(false);
            return _cachedAlerts;
        }

        /// <inheritdoc />
        public async Task<DashboardSecuritySummaryReadModel> GetSecuritySummaryAsync(CancellationToken cancellationToken = default)
        {
            await EnsureCacheValidAsync(cancellationToken).ConfigureAwait(false);
            return _cachedSecurity;
        }

        /// <inheritdoc />
        public async Task<DashboardRecoverySummaryReadModel> GetRecoverySummaryAsync(CancellationToken cancellationToken = default)
        {
            await EnsureCacheValidAsync(cancellationToken).ConfigureAwait(false);
            return _cachedRecovery;
        }

        /// <inheritdoc />
        public async Task<DashboardComplianceSummaryReadModel> GetComplianceSummaryAsync(CancellationToken cancellationToken = default)
        {
            await EnsureCacheValidAsync(cancellationToken).ConfigureAwait(false);
            return _cachedCompliance;
        }

        /// <summary>
        /// Explicitly triggers a manual rebuild of the entire cached dashboard snapshot and read models.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await RebuildSnapshotAndReadModelsAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task EnsureCacheValidAsync(CancellationToken cancellationToken)
        {
            var interval = TimeSpan.FromSeconds(_options.Value.RefreshIntervalSeconds);
            if (DateTime.UtcNow - _lastRefreshTime < interval)
            {
                // Cache is still valid
                return;
            }

            // Hardening optimization: If this is the very first initial load (uninitialized),
            // we must block and wait normally to ensure the snapshot is fully built.
            // Subsequent loads can use the non-blocking stale-while-rebuild strategy.
            if (_lastRefreshTime == DateTime.MinValue)
            {
                await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    // Double check inside lock
                    if (_lastRefreshTime != DateTime.MinValue)
                    {
                        return;
                    }
                    await RebuildSnapshotAndReadModelsAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _cacheLock.Release();
                }
                return;
            }

            // Attempt to acquire cache lock for reload, if someone else has it, return stale immediately (non-blocking)
            bool locked = await _cacheLock.WaitAsync(0, cancellationToken).ConfigureAwait(false);
            if (!locked)
            {
                return;
            }

            try
            {
                // Double-check inside lock
                if (DateTime.UtcNow - _lastRefreshTime < interval)
                {
                    return;
                }

                await RebuildSnapshotAndReadModelsAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task RebuildSnapshotAndReadModelsAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Rebuilding dashboard snapshot and read models...");

            var timestamp = DateTime.UtcNow;

            // --- 1. Query ILiveTelemetryService with Failure Isolation ---
            double cpu = 0.0;
            double mem = 0.0;
            double freeDiskGb = 0.0;
            bool netConnected = true;
            try
            {
                if (_liveTelemetry != null)
                {
                    var telemetryData = await _liveTelemetry.CaptureSnapshotAsync(cancellationToken).ConfigureAwait(false);
                    if (telemetryData != null)
                    {
                        cpu = telemetryData.CpuUsagePercent;
                        if (telemetryData.RamTotalMb > 0)
                        {
                            mem = (telemetryData.RamUsedMb / telemetryData.RamTotalMb) * 100.0;
                        }
                        freeDiskGb = telemetryData.FreeSpaceGb;
                        netConnected = telemetryData.PingMs >= 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard Provider isolated failure in Live Telemetry.");
            }

            // --- 2. Query IPerformanceMonitor with Failure Isolation ---
            double dbLatency = 0.0;
            double ipcLatency = 0.0;
            double tcpLatency = 0.0;
            double diskLatency = 0.0;
            double cacheHit = 0.0;
            double downloadSpeed = 0.0;
            double uploadSpeed = 0.0;
            int queueLen = 0;
            int threadPool = 0;
            int asyncOps = 0;
            int gcCount = 0;
            try
            {
                if (_perfMonitor != null)
                {
                    var perfSnapshot = await _perfMonitor.GetLatestPerformanceSnapshotAsync(cancellationToken).ConfigureAwait(false);
                    if (perfSnapshot != null)
                    {
                        dbLatency = perfSnapshot.DatabaseLatency.TotalMilliseconds;
                        ipcLatency = perfSnapshot.IpcLatency.TotalMilliseconds;
                        tcpLatency = perfSnapshot.TcpLatency.TotalMilliseconds;
                        diskLatency = perfSnapshot.DiskLatency.TotalMilliseconds;
                        cacheHit = perfSnapshot.CacheHitRatio;
                        downloadSpeed = perfSnapshot.DownloadSpeed;
                        uploadSpeed = perfSnapshot.UploadSpeed;
                        queueLen = perfSnapshot.QueueLength;
                        threadPool = perfSnapshot.ThreadPoolThreads;
                        asyncOps = perfSnapshot.AsyncOperationsCount;
                        gcCount = perfSnapshot.GarbageCollectionCount;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard Provider isolated failure in Performance Monitoring.");
            }

            // --- 3. Query IAlertEngine with Failure Isolation ---
            int activeAlertsCount = 0;
            IReadOnlyCollection<AlertRecord> activeAlerts = Array.Empty<AlertRecord>();
            var priorityBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (_alertEngine != null)
                {
                    var resultAlerts = await _alertEngine.GetActiveAlertsAsync(cancellationToken).ConfigureAwait(false);
                    if (resultAlerts != null)
                    {
                        activeAlerts = resultAlerts;
                        activeAlertsCount = activeAlerts.Count;
                        foreach (var alert in activeAlerts)
                        {
                            string priorityStr = alert.Priority.ToString();
                            priorityBreakdown[priorityStr] = priorityBreakdown.GetValueOrDefault(priorityStr) + 1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard Provider isolated failure in Alert Engine.");
            }

            // --- 4. Query ISessionRepository with Failure Isolation ---
            int onlineUsers = 0;
            int runningGames = 0;
            try
            {
                if (_sessionRepo != null)
                {
                    var sessions = await _sessionRepo.GetActiveSessionsAsync().ConfigureAwait(false);
                    if (sessions != null)
                    {
                        var list = sessions.ToList();
                        onlineUsers = list.Select(s => s.UserId).Where(u => !string.IsNullOrEmpty(u)).Distinct().Count();
                        runningGames = list.Count(s => !string.IsNullOrEmpty(s.GameId));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard Provider isolated failure in Session Repository.");
            }

            // --- 5. Query IHealthMonitor with Failure Isolation ---
            IReadOnlyDictionary<string, SubsystemHealthInfo> healthData = new Dictionary<string, SubsystemHealthInfo>();
            string recoverySummary = "System is operational.";
            int failuresCount = 0;
            int totalRecoveriesCount = 0;
            var subsystemFailures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (_healthMonitor != null)
                {
                    healthData = await _healthMonitor.GetDetailedHealthAsync(cancellationToken).ConfigureAwait(false);
                    recoverySummary = await _healthMonitor.GetHealthSummaryAsync(cancellationToken).ConfigureAwait(false);
                    if (healthData != null)
                    {
                        foreach (var kvp in healthData)
                        {
                            failuresCount += kvp.Value.FailureCount;
                            totalRecoveriesCount += kvp.Value.RecoveryCount;
                            if (kvp.Value.FailureCount > 0)
                            {
                                subsystemFailures[kvp.Key] = kvp.Value.FailureCount;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard Provider isolated failure in Health Monitoring.");
            }

            // --- 6. Query ISecurityHardeningService with Failure Isolation ---
            bool integrityVerified = true;
            double policyCompliance = 100.0;
            try
            {
                if (_securityHardening != null)
                {
                    integrityVerified = await _securityHardening.VerifySystemIntegrityAsync(cancellationToken).ConfigureAwait(false);
                    var policyResult = await _securityHardening.ValidatePolicyAsync(cancellationToken).ConfigureAwait(false);
                    if (policyResult != null)
                    {
                        policyCompliance = policyResult.ValidationState == SecurityValidationState.Passed ? 100.0 : 80.0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard Provider isolated failure in Security Hardening.");
            }

            // --- 7. Build the 15 Subsystem Status Models ---
            var subsDict = new Dictionary<string, SubsystemStatus>(StringComparer.OrdinalIgnoreCase);
            string[] subsystemNames = {
                "Authentication", "Database", "Network", "IPC", "Notifications",
                "Downloads", "Updates", "Plugins", "Telemetry", "Recovery",
                "Security", "Policies", "Watchdog", "Overlay", "Synchronization"
            };
            foreach (var name in subsystemNames)
            {
                subsDict[name] = ResolveSubsystemStatus(healthData, name);
            }

            // Determine overall health status string
            string overallHealth = "Healthy";
            if (subsDict.Values.Any(s => s.Health.Equals("Critical", StringComparison.OrdinalIgnoreCase)))
            {
                overallHealth = "Critical";
            }
            else if (subsDict.Values.Any(s => s.Health.Equals("Warning", StringComparison.OrdinalIgnoreCase)))
            {
                overallHealth = "Warning";
            }

            // --- 8. Construct Immutable Read Models ---

            _cachedOverview = new DashboardOverviewReadModel
            {
                LiveMachinesCount = 1, // Local client workstation is 1
                OnlineUsersCount = onlineUsers,
                RunningGamesCount = runningGames,
                OverallHealthStatus = overallHealth,
                Timestamp = timestamp
            };

            _cachedSubsystems = new DashboardSubsystemStatusReadModel
            {
                Authentication = subsDict["Authentication"],
                Database = subsDict["Database"],
                Network = subsDict["Network"],
                IPC = subsDict["IPC"],
                Notifications = subsDict["Notifications"],
                Downloads = subsDict["Downloads"],
                Updates = subsDict["Updates"],
                Plugins = subsDict["Plugins"],
                Telemetry = subsDict["Telemetry"],
                Recovery = subsDict["Recovery"],
                Security = subsDict["Security"],
                Policies = subsDict["Policies"],
                Watchdog = subsDict["Watchdog"],
                Overlay = subsDict["Overlay"],
                Synchronization = subsDict["Synchronization"],
                Subsystems = subsDict,
                Timestamp = timestamp
            };

            _cachedPerformance = new DashboardPerformanceSummaryReadModel
            {
                CpuUsagePercent = Math.Round(cpu, 2),
                MemoryUsagePercent = Math.Round(mem, 2),
                DatabaseLatencyMs = Math.Round(dbLatency, 2),
                IpcLatencyMs = Math.Round(ipcLatency, 2),
                TcpLatencyMs = Math.Round(tcpLatency, 2),
                DiskLatencyMs = Math.Round(diskLatency, 2),
                CacheHitRatio = Math.Round(cacheHit, 2),
                DownloadSpeedBytesPerSec = Math.Round(downloadSpeed, 2),
                UploadSpeedBytesPerSec = Math.Round(uploadSpeed, 2),
                QueueLength = queueLen,
                ThreadPoolThreads = threadPool,
                AsyncOperationsCount = asyncOps,
                GarbageCollectionCount = gcCount,
                Timestamp = timestamp
            };

            _cachedAlerts = new DashboardAlertSummaryReadModel
            {
                ActiveAlertsCount = activeAlertsCount,
                ActiveAlerts = activeAlerts,
                PriorityBreakdown = priorityBreakdown,
                Timestamp = timestamp
            };

            _cachedSecurity = new DashboardSecuritySummaryReadModel
            {
                SecurityViolationsCount = activeAlerts.Count(a => a.Subsystem == Models.Telemetry.Enums.SubsystemType.Security),
                PolicyCompliancePercent = Math.Round(policyCompliance, 2),
                AntiTamperStatus = subsDict["Security"].Health.Equals("Critical", StringComparison.OrdinalIgnoreCase) ? "Failed" : "Enabled",
                KioskSecurityStatus = subsDict["Security"].CurrentStatus.Contains("KioskActive") ? "KioskActive" : "Locked",
                DatabaseEncryptionStatus = subsDict["Database"].CurrentStatus.Contains("Locked") ? "Locked" : "Encrypted",
                SystemIntegrityVerified = integrityVerified,
                Timestamp = timestamp
            };

            _cachedRecovery = new DashboardRecoverySummaryReadModel
            {
                RecoveryStatusSummary = recoverySummary,
                FailuresCount = failuresCount,
                TotalRecoveriesCount = totalRecoveriesCount,
                SubsystemFailures = subsystemFailures,
                Timestamp = timestamp
            };

            _cachedCompliance = new DashboardComplianceSummaryReadModel
            {
                PolicyCompliancePercent = Math.Round(policyCompliance, 2),
                PendingUpdatesCount = subsDict["Updates"].Health.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? 1 : 0, // Simplified pending updates count
                LastPolicyAppliedTimestamp = subsDict["Policies"].LastUpdated.ToString("o"),
                SecurityPoliciesEnforced = integrityVerified,
                Timestamp = timestamp
            };

            // --- 9. Construct and Cache DashboardSnapshot ---
            _cachedSnapshot = new DashboardSnapshot
            {
                Timestamp = timestamp,
                LiveMachinesCount = _cachedOverview.LiveMachinesCount,
                OnlineUsersCount = _cachedOverview.OnlineUsersCount,
                RunningGamesCount = _cachedOverview.RunningGamesCount,
                CpuUsagePercent = _cachedPerformance.CpuUsagePercent,
                MemoryUsagePercent = _cachedPerformance.MemoryUsagePercent,
                FailuresCount = _cachedRecovery.FailuresCount,
                ActiveAlertsCount = _cachedAlerts.ActiveAlertsCount,
                DownloadsSpeedBytesPerSec = _cachedPerformance.DownloadSpeedBytesPerSec,
                PendingUpdatesCount = _cachedCompliance.PendingUpdatesCount,
                NetworkConnected = netConnected,
                PolicyCompliancePercent = _cachedCompliance.PolicyCompliancePercent,
                RecoveryStatusSummary = _cachedRecovery.RecoveryStatusSummary,
                SecurityViolationsCount = _cachedSecurity.SecurityViolationsCount
            };

            _lastRefreshTime = timestamp;
            _logger.LogDebug("Dashboard snapshot and read models rebuilt successfully.");
        }

        private SubsystemStatus ResolveSubsystemStatus(IReadOnlyDictionary<string, SubsystemHealthInfo> healthData, string subsystemName)
        {
            SubsystemHealthInfo? info = null;
            if (healthData != null)
            {
                if (healthData.TryGetValue(subsystemName, out info)) { }
                else
                {
                    // Check case-insensitive
                    foreach (var kvp in healthData)
                    {
                        if (kvp.Key.Equals(subsystemName, StringComparison.OrdinalIgnoreCase))
                        {
                            info = kvp.Value;
                            break;
                        }
                    }
                }
            }

            if (info != null)
            {
                var issuesList = new List<string>();
                if (!string.IsNullOrEmpty(info.LastException))
                {
                    issuesList.Add(info.LastException);
                }
                if (info.FailureCount > 0)
                {
                    issuesList.Add($"Experienced {info.FailureCount} failures.");
                }

                return new SubsystemStatus
                {
                    SubsystemName = subsystemName,
                    Health = info.State.ToString(),
                    CurrentStatus = !string.IsNullOrEmpty(info.LastMessage) ? info.LastMessage : "Operational",
                    LastUpdated = info.LastUpdated,
                    ActiveIssues = issuesList.ToArray()
                };
            }

            // Resilient Fallback
            return new SubsystemStatus
            {
                SubsystemName = subsystemName,
                Health = "Healthy",
                CurrentStatus = "Operational",
                LastUpdated = DateTime.UtcNow,
                ActiveIssues = Array.Empty<string>()
            };
        }
    }
}
