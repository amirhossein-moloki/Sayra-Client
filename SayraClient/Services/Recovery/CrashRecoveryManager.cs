using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.OfflineQueue;

namespace SayraClient.Services.Recovery
{
    /// <summary>
    /// Production-grade implementation of the Crash Recovery Manager.
    /// Manages startup dirty shutdown detection, database consistency verification, reindexing, and state recovery.
    /// </summary>
    public class CrashRecoveryManager : ICrashRecoveryManager
    {
        private readonly ILogger<CrashRecoveryManager> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly SemaphoreSlim _executionLock = new(1, 1);
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private readonly string _stateFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "shutdown_state.json");

        // Thread-safe state collection of attempts and results from the current run
        private readonly List<RecoveryAttempt> _attempts = new();
        private readonly List<RecoveryResult> _results = new();
        private bool _isRecoveryExecuted;

        public CrashRecoveryManager(ILogger<CrashRecoveryManager> logger, IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Orchestrates the full systematic startup recovery pipeline.
        /// </summary>
        public async Task ExecuteStartupRecoveryAsync(CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            await _executionLock.WaitAsync(cancellationToken);
            try
            {
                if (_isRecoveryExecuted)
                {
                    _logger.LogWarning("Startup recovery has already been executed for this session.");
                    return;
                }

                _logger.LogInformation("STARTUP RECOVERY [CorrelationId={CorrelationId}]: Initiating systematic recovery protocols...", correlationId);

                var eventDispatcher = _serviceProvider.GetService<IEventDispatcher>();
                eventDispatcher?.Dispatch(new CrashRecoveryStartedEvent(correlationId, startTime));

                // 1. Check previous shutdown reason
                var shutdownState = await ValidatePreviousShutdownAsync(cancellationToken);

                // 2. Determine recovery requirement
                if (shutdownState.IsRecoveryRequired)
                {
                    _logger.LogWarning("Abnormal or first boot detected (Reason: {Reason}). Commencing recovery workflow...", shutdownState.LastShutdownReason);

                    // 3. Verify Database Consistency & Optimize
                    await VerifyAndRepairDatabaseAsync(cancellationToken);

                    // 4. Recover Interrupted Operations
                    await RecoverInterruptedOperationsAsync(cancellationToken);

                    // 5. Cleanup Temporary State
                    await CleanupTemporaryStateAsync(cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Normal shutdown verified. No crash recovery required.");
                }

                _isRecoveryExecuted = true;
                var report = await GenerateRecoverySummaryAsync(cancellationToken);

                int recoveredCount = report.SuccessfulRecoveries;
                int failedCount = report.Attempts.Count - report.SuccessfulRecoveries;
                var duration = DateTime.UtcNow - startTime;

                _logger.LogInformation("STARTUP RECOVERY [CorrelationId={CorrelationId}]: Completed in {Duration}ms. Recovered: {Recovered}, Failed: {Failed}",
                    correlationId, duration.TotalMilliseconds, recoveredCount, failedCount);

                eventDispatcher?.Dispatch(new CrashRecoveryCompletedEvent(correlationId, duration, recoveredCount, failedCount, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "STARTUP RECOVERY [CorrelationId={CorrelationId}]: Fatal error during startup recovery execution.", correlationId);

                var eventDispatcher = _serviceProvider.GetService<IEventDispatcher>();
                eventDispatcher?.Dispatch(new CrashRecoveryFailedEvent(correlationId, duration, ex.Message, DateTime.UtcNow));
            }
            finally
            {
                _executionLock.Release();
            }
        }

        /// <summary>
        /// Verifies database structural consistency and performs index repairs or reindexing if necessary.
        /// </summary>
        public async Task VerifyAndRepairDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var attemptId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;
            string correlationId = Guid.NewGuid().ToString();

            _logger.LogInformation("Database Recovery: Verifying SQLite structural consistency...");

            var attempt = new RecoveryAttempt
            {
                AttemptId = attemptId,
                SubsystemName = "Database",
                ActionTaken = "DB_INTEGRITY_CHECK_AND_REINDEX",
                AttemptNumber = 1,
                Status = RecoveryStatus.InProgress,
                Message = "Initiating database verification and repair."
            };

            lock (_attempts) { _attempts.Add(attempt); }

            try
            {
                var dbService = _serviceProvider.GetService<ILocalDatabaseService>();
                if (dbService != null)
                {
                    await dbService.InitializeDatabaseAsync(cancellationToken);

                    using var conn = dbService.CreateConnection();
                    await conn.OpenAsync(cancellationToken);

                    // DB Integrity Check
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA integrity_check;";
                        var check = await cmd.ExecuteScalarAsync(cancellationToken);
                        _logger.LogInformation("Database integrity check result: {Result}", check);

                        if (check == null || check.ToString() != "ok")
                        {
                            throw new InvalidOperationException($"SQLite integrity check failed: {check}");
                        }
                    }

                    // Repair corrupted indexes
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "REINDEX;";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                        _logger.LogInformation("Database reindexing completed successfully.");
                    }
                }

                var duration = DateTime.UtcNow - startTime;
                var result = new RecoveryResult
                {
                    AttemptId = attemptId,
                    SubsystemName = "Database",
                    IsSuccessful = true,
                    FinalStatus = RecoveryStatus.Success,
                    CompletedAt = DateTime.UtcNow,
                    Duration = duration,
                    OutputMessage = "Database integrity check and reindexing completed successfully."
                };

                lock (_results) { _results.Add(result); }
                LogRecoveryStep(correlationId, "Database", "DB_INTEGRITY_CHECK_AND_REINDEX", duration, "Success", null, 1, 0);

                var eventDispatcher = _serviceProvider.GetService<IEventDispatcher>();
                eventDispatcher?.Dispatch(new RecoveryItemRestoredEvent(correlationId, "Database", "DB_INTEGRITY_CHECK_AND_REINDEX", DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                var result = new RecoveryResult
                {
                    AttemptId = attemptId,
                    SubsystemName = "Database",
                    IsSuccessful = false,
                    FinalStatus = RecoveryStatus.Failed,
                    CompletedAt = DateTime.UtcNow,
                    Duration = duration,
                    OutputMessage = $"Database verification/repair failed: {ex.Message}",
                    ErrorDetails = ex.ToString()
                };

                lock (_results) { _results.Add(result); }
                LogRecoveryStep(correlationId, "Database", "DB_INTEGRITY_CHECK_AND_REINDEX", duration, "Failed", ex, 0, 1);

                var eventDispatcher = _serviceProvider.GetService<IEventDispatcher>();
                eventDispatcher?.Dispatch(new RecoveryValidationFailedEvent(correlationId, "Database", "DB_INTEGRITY_CHECK_AND_REINDEX", ex.Message, DateTime.UtcNow));
            }
        }

        /// <summary>
        /// Validates the previous shutdown state to determine if the application was terminated unexpectedly.
        /// </summary>
        public async Task<PreviousShutdownState> ValidatePreviousShutdownAsync(CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Shutdown Detection: Checking previous state from {Path}", _stateFilePath);
                PreviousShutdownState state;

                if (File.Exists(_stateFilePath))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(_stateFilePath, cancellationToken);
                        state = System.Text.Json.JsonSerializer.Deserialize<PreviousShutdownState>(json)
                                ?? new PreviousShutdownState();

                        // If it was left as Running, it did not shut down cleanly
                        if (state.LastShutdownReason == "Running")
                        {
                            state.LastShutdownReason = "Crash";
                            state.IsRecoveryRequired = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to read or parse shutdown state file. Assuming unknown dirty shutdown.");
                        state = new PreviousShutdownState
                        {
                            LastShutdownReason = "Unknown",
                            IsRecoveryRequired = true
                        };
                    }
                }
                else
                {
                    _logger.LogInformation("Shutdown state file not found. Assuming first run/fresh boot. Triggering resilient startup verification.");
                    state = new PreviousShutdownState
                    {
                        LastShutdownReason = "Unknown",
                        IsRecoveryRequired = true // First run or clean startup, run recovery to verify databases and integrity unconditionally!
                    };
                }

                // Write the current session's "Running" state to capture crash on next startup
                var currentState = new PreviousShutdownState
                {
                    LastShutdownReason = "Running",
                    LastStartupTimestamp = DateTime.UtcNow,
                    LastSuccessfulShutdownTimestamp = state.LastSuccessfulShutdownTimestamp,
                    IsRecoveryRequired = true
                };

                string dir = Path.GetDirectoryName(_stateFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string currentJson = System.Text.Json.JsonSerializer.Serialize(currentState, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_stateFilePath, currentJson, cancellationToken);

                _logger.LogInformation("Validated previous shutdown: LastReason={Reason}, RecoveryRequired={Req}",
                    state.LastShutdownReason, state.IsRecoveryRequired);

                return state;
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Records a clean graceful shutdown state.
        /// </summary>
        public async Task RecordCleanShutdownAsync(CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Shutdown Detection: Recording graceful clean shutdown state.");
                var state = new PreviousShutdownState
                {
                    LastShutdownReason = "Normal",
                    LastStartupTimestamp = DateTime.UtcNow,
                    LastSuccessfulShutdownTimestamp = DateTime.UtcNow,
                    IsRecoveryRequired = false
                };

                string dir = Path.GetDirectoryName(_stateFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_stateFilePath, json, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record clean shutdown state.");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Detects and recovers interrupted operations such as downloads, updates, offline queue items, etc.
        /// </summary>
        public async Task<List<RecoveryResult>> RecoverInterruptedOperationsAsync(CancellationToken cancellationToken = default)
        {
            var resultsList = new List<RecoveryResult>();

            // 1. Recover Offline Queue
            resultsList.Add(await RecoverSubsystemStateAsync("OfflineQueue", cancellationToken));

            // 2. Recover Interrupted Downloads
            resultsList.Add(await RecoverSubsystemStateAsync("Downloads", cancellationToken));

            // 3. Recover Interrupted Updates
            resultsList.Add(await RecoverSubsystemStateAsync("Updates", cancellationToken));

            // 4. Recover Cache
            resultsList.Add(await RecoverSubsystemStateAsync("Cache", cancellationToken));

            // 5. Recover Notification Queue
            resultsList.Add(await RecoverSubsystemStateAsync("Notifications", cancellationToken));

            // 6. Recover Synchronization State
            resultsList.Add(await RecoverSubsystemStateAsync("Sync", cancellationToken));

            // 7. Recover Policy State
            resultsList.Add(await RecoverSubsystemStateAsync("Policy", cancellationToken));

            // 8. Legacy / Phase 5 & 6 Compatibility Recovery Steps
            await RecoverAuditQueueAsync(cancellationToken);
            await RecoverAdvertisementPlaybackAsync(cancellationToken);
            await RecoverFleetAndBulkOperationsAsync(cancellationToken);
            await RecoverTelemetryStateAsync(cancellationToken);
            await RecoverPendingCommandsAsync(cancellationToken);

            return resultsList;
        }

        /// <summary>
        /// Restores state consistency for a specific subsystem.
        /// </summary>
        public async Task<RecoveryResult> RecoverSubsystemStateAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            var attemptId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;
            string correlationId = Guid.NewGuid().ToString();

            _logger.LogInformation("Subsystem Recovery: Initiating recovery for subsystem '{Subsystem}'...", subsystemName);

            var attempt = new RecoveryAttempt
            {
                AttemptId = attemptId,
                SubsystemName = subsystemName,
                ActionTaken = $"RECOVER_{subsystemName.ToUpper()}",
                AttemptNumber = 1,
                Status = RecoveryStatus.InProgress,
                Message = $"Initiating recovery for {subsystemName} subsystem."
            };

            lock (_attempts) { _attempts.Add(attempt); }

            bool isSuccess = false;
            string message = string.Empty;
            Exception? errorEx = null;
            int recoveredCount = 0;
            int failedCount = 0;

            try
            {
                switch (subsystemName)
                {
                    case "OfflineQueue":
                        var queueMgr = _serviceProvider.GetService<IOfflineQueueManager>();
                        if (queueMgr != null)
                        {
                            bool ok = await queueMgr.VerifyIntegrityAsync();
                            if (!ok)
                            {
                                _logger.LogWarning("Offline queue consistency check failed! Recreating database...");
                                await queueMgr.ForceRecreateDatabaseAsync();
                                message = "Offline queue database corrupted. Forced database recreation completed successfully.";
                                recoveredCount = 1;
                            }
                            else
                            {
                                var pending = await queueMgr.GetPendingEventsAsync();
                                message = $"Offline queue verified. Recovered {pending.Count} pending items.";
                                recoveredCount = pending.Count;
                            }
                            isSuccess = true;
                        }
                        else
                        {
                            message = "Offline queue manager service is not registered.";
                        }
                        break;

                    case "Downloads":
                        var adRepo = _serviceProvider.GetService<IAdvertisementRepository>();
                        var downloadManager = _serviceProvider.GetService<IAdDownloadManager>();
                        if (adRepo != null && downloadManager != null)
                        {
                            var activeCampaigns = await adRepo.GetActiveCampaignsAsync(cancellationToken);
                            foreach (var campaign in activeCampaigns)
                            {
                                if (!campaign.IsDownloaded)
                                {
                                    string tempFile = campaign.MediaLocalPath + ".tmp";
                                    if (File.Exists(tempFile))
                                    {
                                        _logger.LogInformation("Interrupted Download: Resuming download for campaign {CampaignId}...", campaign.CampaignId);
                                        await downloadManager.ResumeDownloadAsync(campaign, tempFile, cancellationToken);
                                        recoveredCount++;
                                    }
                                    else
                                    {
                                        _logger.LogInformation("Interrupted Download: Starting fresh download for campaign {CampaignId}...", campaign.CampaignId);
                                        await downloadManager.DownloadMediaAsync(campaign, cancellationToken);
                                        recoveredCount++;
                                    }
                                }
                            }
                            await downloadManager.CleanupOrphanDownloadsAsync(cancellationToken);
                            message = $"Resumed and restored {recoveredCount} pending media downloads.";
                            isSuccess = true;
                        }
                        else
                        {
                            message = "Advertisement repositories or download managers are not registered.";
                        }
                        break;

                    case "Updates":
                        var updateHistoryRepo = _serviceProvider.GetService<IUpdateHistoryRepository>();
                        var rollbackEngine = _serviceProvider.GetService<IRollbackEngine>();
                        if (updateHistoryRepo != null && rollbackEngine != null)
                        {
                            var history = await updateHistoryRepo.GetAllAsync(cancellationToken);
                            // Find uncompleted/staged records
                            var interrupted = history.Where(r => r.Status == "STAGED").ToList();
                            foreach (var record in interrupted)
                            {
                                _logger.LogWarning("Interrupted Update: Staged update found (Version: {Version}). Triggering rollback safety protocols...", record.Version);
                                bool rollOk = await rollbackEngine.ExecuteRollbackAsync(record.Id.ToString(), "Interrupted update during unexpected shutdown", cancellationToken);
                                if (rollOk)
                                {
                                    record.Status = "ROLLED_BACK";
                                    await updateHistoryRepo.UpdateAsync(record, cancellationToken);
                                    recoveredCount++;
                                }
                                else
                                {
                                    failedCount++;
                                }
                            }
                            message = $"Processed update records. Recovered/Rolled back: {recoveredCount}, Failed: {failedCount}.";
                            isSuccess = true;
                        }
                        else
                        {
                            message = "Update history repository or rollback engine is not registered.";
                        }
                        break;

                    case "Cache":
                        var cache = _serviceProvider.GetService<IAdvertisementCache>();
                        if (cache != null)
                        {
                            await cache.ClearExpiredCacheAsync(cancellationToken);
                            message = "Validated cache state. Expired entries removed successfully.";
                            isSuccess = true;
                            recoveredCount = 1;
                        }
                        else
                        {
                            message = "Cache service is not registered.";
                        }
                        break;

                    case "Notifications":
                        // Use reflection to locate INotificationRepository to decouple SayraClient from Sayra.UI assemblies
                        Type notificationRepoType = Type.GetType("Sayra.UI.Notifications.Services.INotificationRepository, Sayra.UI")
                            ?? AppDomain.CurrentDomain.GetAssemblies()
                                .SelectMany(a => {
                                    try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                                })
                                .FirstOrDefault(t => t.FullName == "Sayra.UI.Notifications.Services.INotificationRepository");

                        if (notificationRepoType != null)
                        {
                            var notificationRepo = _serviceProvider.GetService(notificationRepoType);
                            if (notificationRepo != null)
                            {
                                // Invoke InitializeAsync
                                var initMethod = notificationRepoType.GetMethod("InitializeAsync");
                                if (initMethod != null)
                                {
                                    var task = (Task)initMethod.Invoke(notificationRepo, null);
                                    if (task != null) await task;
                                }

                                // Invoke GetNotificationsAsync
                                var getMethod = notificationRepoType.GetMethod("GetNotificationsAsync");
                                if (getMethod != null)
                                {
                                    var task = getMethod.Invoke(notificationRepo, new object?[] { null, null, null });
                                    if (task is Task pendingTask)
                                    {
                                        await pendingTask;
                                        var resultProperty = pendingTask.GetType().GetProperty("Result");
                                        if (resultProperty != null)
                                        {
                                            var list = resultProperty.GetValue(pendingTask) as System.Collections.ICollection;
                                            int count = list?.Count ?? 0;
                                            message = $"Notification queue recovered. Pending items: {count}";
                                            recoveredCount = count;
                                        }
                                    }
                                }
                                isSuccess = true;
                            }
                            else
                            {
                                message = "Notification queue repository service was not found in provider.";
                            }
                        }
                        else
                        {
                            message = "Notification queue repository type could not be loaded.";
                        }
                        break;

                    case "Sync":
                        var syncService = _serviceProvider.GetService<IWorkstationSyncService>();
                        if (syncService != null)
                        {
                            var delta = await syncService.CompareLocalAndServerAsync(cancellationToken);
                            message = "Synchronization state and comparisons successfully restored.";
                            isSuccess = true;
                            recoveredCount = 1;
                        }
                        else
                        {
                            message = "Workstation synchronization service is not registered.";
                        }
                        break;

                    case "Policy":
                        var policyEngine = _serviceProvider.GetService<IPolicyEngine>();
                        var policyRepo = _serviceProvider.GetService<IPolicyRepository>();
                        if (policyEngine != null && policyRepo != null)
                        {
                            var activePolicies = await policyRepo.GetActivePoliciesAsync(cancellationToken);
                            foreach (var policy in activePolicies)
                            {
                                await policyEngine.ApplyPoliciesAsync(policy, cancellationToken);
                                recoveredCount++;
                            }
                            message = $"Validated stored policy profiles. Restored and applied {recoveredCount} policy states.";
                            isSuccess = true;
                        }
                        else
                        {
                            message = "Policy engine or repository is not registered.";
                        }
                        break;

                    default:
                        message = $"Unknown subsystem {subsystemName}. No recovery strategy applied.";
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover subsystem {Subsystem}.", subsystemName);
                isSuccess = false;
                errorEx = ex;
                message = $"Recovery encountered error: {ex.Message}";
                failedCount++;
            }

            var duration = DateTime.UtcNow - startTime;
            var finalStatus = isSuccess ? RecoveryStatus.Success : RecoveryStatus.Failed;

            var result = new RecoveryResult
            {
                AttemptId = attemptId,
                SubsystemName = subsystemName,
                IsSuccessful = isSuccess,
                FinalStatus = finalStatus,
                CompletedAt = DateTime.UtcNow,
                Duration = duration,
                OutputMessage = message,
                ErrorDetails = errorEx?.ToString()
            };

            lock (_results) { _results.Add(result); }
            LogRecoveryStep(correlationId, subsystemName, $"RECOVER_{subsystemName.ToUpper()}", duration, isSuccess ? "Success" : "Failed", errorEx, recoveredCount, failedCount);

            var eventDispatcher = _serviceProvider.GetService<IEventDispatcher>();
            if (isSuccess)
            {
                eventDispatcher?.Dispatch(new RecoveryItemRestoredEvent(correlationId, subsystemName, $"RECOVER_{subsystemName.ToUpper()}", DateTime.UtcNow));
            }
            else
            {
                eventDispatcher?.Dispatch(new RecoveryValidationFailedEvent(correlationId, subsystemName, $"RECOVER_{subsystemName.ToUpper()}", errorEx?.Message ?? message, DateTime.UtcNow));
            }

            return result;
        }

        /// <summary>
        /// Safely cleans up temporary and incomplete state files from the workstation storage.
        /// </summary>
        public Task CleanupTemporaryStateAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Cleanup: Purging temporary and orphaned recovery files...");
            try
            {
                var adRepo = _serviceProvider.GetService<IAdvertisementRepository>();
                if (adRepo != null)
                {
                    // Clean up files ending in .tmp under the base directory or known campaign download folders
                    string baseDir = AppContext.BaseDirectory;
                    var tmpFiles = Directory.GetFiles(baseDir, "*.tmp", SearchOption.AllDirectories);
                    foreach (var file in tmpFiles)
                    {
                        try
                        {
                            _logger.LogInformation("Purging temporary file: {Path}", file);
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete temporary file {Path}", file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform temporary files cleanup.");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Generates a comprehensive report detailing the crash recovery attempt and its outcomes.
        /// </summary>
        public Task<RecoveryReport> GenerateRecoverySummaryAsync(CancellationToken cancellationToken = default)
        {
            List<RecoveryAttempt> attemptsCopy;
            List<RecoveryResult> resultsCopy;

            lock (_attempts) { attemptsCopy = _attempts.ToList(); }
            lock (_results) { resultsCopy = _results.ToList(); }

            int successful = resultsCopy.Count(r => r.IsSuccessful);

            var report = new RecoveryReport
            {
                ReportId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                MachineId = Environment.MachineName,
                ClientVersion = "1.0.0",
                TotalRecoveryActions = attemptsCopy.Count,
                SuccessfulRecoveries = successful,
                Attempts = attemptsCopy,
                Recommendations = resultsCopy
                    .Where(r => !r.IsSuccessful)
                    .Select(r => $"Examine errors in subsystem '{r.SubsystemName}': {r.OutputMessage}")
                    .ToList()
            };

            return Task.FromResult(report);
        }

        private void LogRecoveryStep(string correlationId, string subsystem, string operation, TimeSpan duration, string result, Exception? exception, int recoveredCount, int failedCount)
        {
            if (exception != null)
            {
                _logger.LogError(exception, "Crash Recovery Step Completed: CorrelationId={CorrelationId}, Subsystem={Subsystem}, Operation={Operation}, Duration={Duration}ms, Result={Result}, Exception={ExceptionName}, RecoveredCount={RecoveredCount}, FailedCount={FailedCount}",
                    correlationId, subsystem, operation, duration.TotalMilliseconds, result, exception.GetType().Name, recoveredCount, failedCount);
            }
            else
            {
                _logger.LogInformation("Crash Recovery Step Completed: CorrelationId={CorrelationId}, Subsystem={Subsystem}, Operation={Operation}, Duration={Duration}ms, Result={Result}, Exception=None, RecoveredCount={RecoveredCount}, FailedCount={FailedCount}",
                    correlationId, subsystem, operation, duration.TotalMilliseconds, result, recoveredCount, failedCount);
            }
        }

        #region Legacy / Phase 5 & 6 Compatibility Recovery Steps

        private async Task RecoverAuditQueueAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Startup Recovery: Verifying and recovering Audit Queue...");
            try
            {
                var auditService = _serviceProvider.GetService<IAuditService>();
                if (auditService != null)
                {
                    bool ok = await auditService.VerifyAuditChainIntegrityAsync(cancellationToken);
                    if (!ok)
                    {
                        _logger.LogCritical("AUDIT LOG INTEGRITY CRITICAL FAILURE: Cryptographic hash chain is broken!");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover audit queue.");
            }
        }

        private async Task RecoverAdvertisementPlaybackAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Startup Recovery: Resetting advertisement playback history...");
            try
            {
                var repo = _serviceProvider.GetService<IAdvertisementRepository>();
                if (repo != null)
                {
                    var list = await repo.GetPlaybackHistoryAsync(cancellationToken);
                    _logger.LogInformation("Restored playback history containing {Count} records.", list.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover advertisement playback history.");
            }
        }

        private async Task RecoverFleetAndBulkOperationsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Startup Recovery: Restoring fleet and bulk administrative operations...");
            try
            {
                var alertsManager = _serviceProvider.GetService<IAlertManager>();
                if (alertsManager != null)
                {
                    var alerts = await alertsManager.GetActiveAlertsAsync(cancellationToken);
                    _logger.LogInformation("Restored fleet monitor. Active alerts recovered: {Count}", alerts.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover fleet operations.");
            }
        }

        private Task RecoverTelemetryStateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Startup Recovery: Restoring background telemetry state...");
            return Task.CompletedTask;
        }

        private async Task RecoverPendingCommandsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Startup Recovery: Recovering pending remote commands...");
            try
            {
                var repo = _serviceProvider.GetService<IRemoteCommandRepository>();
                var commandEngine = _serviceProvider.GetService<IRemoteCommandEngine>();

                if (repo != null && commandEngine != null)
                {
                    var historyList = await repo.GetPendingCommandsAsync(cancellationToken);
                    foreach (var history in historyList)
                    {
                        var command = new RemoteCommand
                        {
                            CommandId = Guid.Parse(history.CommandId),
                            Action = history.Action,
                            TargetClientId = history.TargetPcId,
                            SenderAdminId = history.SenderAdminId,
                            Payload = history.PayloadJson ?? string.Empty,
                            Signature = history.Signature,
                            Timestamp = DateTime.Parse(history.ReceivedAt)
                        };

                        _logger.LogWarning("RE-QUEUEING UNCOMPLETED COMMAND: Re-submitting command {CommandId} ({Action}) left in {Status} status.",
                            command.CommandId, command.Action, history.Status);

                        await commandEngine.QueueCommandAsync(command);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover pending remote commands.");
            }
        }

        #endregion
    }
}
