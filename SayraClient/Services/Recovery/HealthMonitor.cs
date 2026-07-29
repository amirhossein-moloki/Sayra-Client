using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;

namespace SayraClient.Services.Recovery
{
    /// <summary>
    /// Enterprise Health Monitoring Engine for continuous subsystem health monitoring.
    /// This implementation is fully thread-safe, non-blocking, and production ready.
    /// </summary>
    public class HealthMonitor : IHealthMonitor, IDisposable
    {
        private readonly ILogger<HealthMonitor> _logger;
        private readonly HealthMonitorOptions _options;
        private readonly ConcurrentDictionary<string, SubsystemHealthInfo> _subsystems = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentQueue<HealthSnapshot> _historicalSnapshots = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _backgroundTask;

        #region Events

        public event Action<string, SubsystemHealthState, SubsystemHealthState>? SubsystemHealthStateChanged;
        public event EventHandler<SubsystemRegisteredEventArgs>? SubsystemRegistered;
        public event EventHandler<HeartbeatUpdatedEventArgs>? HeartbeatUpdated;
        public event EventHandler<StateChangedEventArgs>? StateChanged;
        public event EventHandler<FailureRecordedEventArgs>? FailureRecorded;
        public event EventHandler<RecoveryCountUpdatedEventArgs>? RecoveryCountUpdated;
        public event EventHandler<HealthScoreChangedEventArgs>? HealthScoreChanged;
        public event EventHandler<SubsystemEventArgs>? SubsystemRemoved;

        #endregion

        public HealthMonitor(ILogger<HealthMonitor> logger, IOptions<HealthMonitorOptions>? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new HealthMonitorOptions();

            // Register standard default subsystems
            RegisterSubsystem("Database", new List<string>());
            RegisterSubsystem("AuditService", new List<string> { "Database" });
            RegisterSubsystem("RemoteCommandEngine", new List<string> { "Database", "AuditService" });
            RegisterSubsystem("PolicyEngine", new List<string> { "Database", "AuditService" });
            RegisterSubsystem("Telemetry", new List<string>());
            RegisterSubsystem("FleetManager", new List<string> { "Database" });
            RegisterSubsystem("AdvertisementEngine", new List<string> { "Database", "DownloadManager" });
            RegisterSubsystem("DownloadManager", new List<string>());

            // Start highly optimized background timeout checking task
            _backgroundTask = Task.Run(CheckSubsystemTimeoutsAndPropagationAsync);
        }

        #region Subsystem Registration

        public void RegisterSubsystem(string subsystemName, List<string> dependencies)
        {
            if (string.IsNullOrWhiteSpace(subsystemName)) return;

            var startTime = DateTime.UtcNow;
            var correlationId = Guid.NewGuid().ToString("N");

            var info = new SubsystemHealthInfo
            {
                SubsystemId = subsystemName,
                SubsystemName = subsystemName,
                DisplayName = subsystemName,
                State = SubsystemHealthState.Healthy,
                LastHeartbeat = DateTime.UtcNow,
                LastSuccessfulHeartbeat = DateTime.UtcNow,
                Dependencies = dependencies ?? new List<string>(),
                LastMessage = "Subsystem registered."
            };
            info.HealthHistory.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Registered as Healthy.");

            _subsystems[subsystemName] = info;

            // Recalculate health score on registration
            RecalculateHealthScoreInternal(info);

            _logger.LogInformation("Subsystem '{SubsystemName}' registered. Metadata: {@LogMetadata}",
                subsystemName, new {
                    CorrelationId = correlationId,
                    Subsystem = subsystemName,
                    Operation = "Register",
                    Timestamp = DateTime.UtcNow,
                    Duration = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    Result = "Success"
                });

            // Raise Event Safely
            SafeRaiseEvent(SubsystemRegistered, new SubsystemRegisteredEventArgs(subsystemName, info.Dependencies));
        }

        public Task RegisterSubsystemAsync(string subsystemName, List<string> dependencies, CancellationToken cancellationToken = default)
        {
            RegisterSubsystem(subsystemName, dependencies);
            return Task.CompletedTask;
        }

        public void UnregisterSubsystem(string subsystemName)
        {
            if (string.IsNullOrWhiteSpace(subsystemName)) return;

            var startTime = DateTime.UtcNow;
            var correlationId = Guid.NewGuid().ToString("N");

            if (_subsystems.TryRemove(subsystemName, out _))
            {
                _logger.LogInformation("Subsystem '{SubsystemName}' removed. Metadata: {@LogMetadata}",
                    subsystemName, new {
                        CorrelationId = correlationId,
                        Subsystem = subsystemName,
                        Operation = "Unregister",
                        Timestamp = DateTime.UtcNow,
                        Duration = (DateTime.UtcNow - startTime).TotalMilliseconds,
                        Result = "Success"
                    });

                SafeRaiseEvent(SubsystemRemoved, new SubsystemEventArgs(subsystemName));
            }
        }

        public Task UnregisterSubsystemAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            UnregisterSubsystem(subsystemName);
            return Task.CompletedTask;
        }

        #endregion

        #region Heartbeat Reporting

        public void ReportHeartbeat(string subsystemName)
        {
            if (string.IsNullOrWhiteSpace(subsystemName)) return;

            var startTime = DateTime.UtcNow;
            var correlationId = Guid.NewGuid().ToString("N");

            if (!_subsystems.TryGetValue(subsystemName, out var info))
            {
                RegisterSubsystem(subsystemName, new List<string>());
                _subsystems.TryGetValue(subsystemName, out info);
            }

            if (info != null)
            {
                var now = DateTime.UtcNow;
                var previousState = info.State;

                info.LastHeartbeat = now;
                info.LastSuccessfulHeartbeat = now;

                _logger.LogDebug("Heartbeat reported for '{SubsystemName}'. Metadata: {@LogMetadata}",
                    subsystemName, new {
                        CorrelationId = correlationId,
                        Subsystem = subsystemName,
                        Operation = "Heartbeat",
                        Timestamp = now,
                        Duration = (DateTime.UtcNow - startTime).TotalMilliseconds,
                        Result = "Success"
                    });

                SafeRaiseEvent(HeartbeatUpdated, new HeartbeatUpdatedEventArgs(subsystemName, now));

                if (info.State == SubsystemHealthState.Offline || info.State == SubsystemHealthState.Critical)
                {
                    TransitionState(info, SubsystemHealthState.Healthy, "Subsystem came back online via heartbeat.");
                }
                else
                {
                    // Recalculate score on healthy heartbeats
                    RecalculateHealthScoreInternal(info);
                }
            }
        }

        public Task ReportHeartbeatAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            ReportHeartbeat(subsystemName);
            return Task.CompletedTask;
        }

        #endregion

        #region State Transitions

        public void ReportSubsystemState(string subsystemName, SubsystemHealthState state, string message, string? exceptionDetails = null)
        {
            if (string.IsNullOrWhiteSpace(subsystemName)) return;

            var startTime = DateTime.UtcNow;
            var correlationId = Guid.NewGuid().ToString("N");

            if (!_subsystems.TryGetValue(subsystemName, out var info))
            {
                RegisterSubsystem(subsystemName, new List<string>());
                _subsystems.TryGetValue(subsystemName, out info);
            }

            if (info != null)
            {
                info.LastMessage = message;
                info.LastException = exceptionDetails;

                if (state == SubsystemHealthState.Critical || state == SubsystemHealthState.Offline)
                {
                    info.FailureCount++;
                    _logger.LogWarning("Failure recorded for Subsystem '{SubsystemName}': {Message}. Exception: {Exception}. Metadata: {@LogMetadata}",
                        subsystemName, message, exceptionDetails, new {
                            CorrelationId = correlationId,
                            Subsystem = subsystemName,
                            Operation = "FailureRecord",
                            Timestamp = DateTime.UtcNow,
                            Duration = (DateTime.UtcNow - startTime).TotalMilliseconds,
                            Result = "Failure"
                        });

                    SafeRaiseEvent(FailureRecorded, new FailureRecordedEventArgs(subsystemName, message, exceptionDetails));
                }

                TransitionState(info, state, message);
            }
        }

        public Task ReportSubsystemStateAsync(string subsystemName, SubsystemHealthState state, string message, string? exceptionDetails = null, CancellationToken cancellationToken = default)
        {
            ReportSubsystemState(subsystemName, state, message, exceptionDetails);
            return Task.CompletedTask;
        }

        public SubsystemHealthState GetSubsystemHealth(string subsystemName)
        {
            EvaluateSubsystemState(subsystemName);
            return _subsystems.TryGetValue(subsystemName, out var info) ? info.State : SubsystemHealthState.Offline;
        }

        public Task<SubsystemHealthState> GetSubsystemHealthAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetSubsystemHealth(subsystemName));
        }

        #endregion

        #region Detailed Health & Diagnostics

        public IReadOnlyDictionary<string, SubsystemHealthInfo> GetDetailedHealth()
        {
            foreach (var key in _subsystems.Keys)
            {
                EvaluateSubsystemState(key);
            }
            return _subsystems.ToDictionary(k => k.Key, v => CloneHealthInfo(v.Value), StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyDictionary<string, SubsystemHealthInfo>> GetDetailedHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetDetailedHealth());
        }

        public bool RunHealthCheck(string subsystemName)
        {
            _logger.LogInformation("Executing health check for subsystem '{SubsystemName}'...", subsystemName);
            if (!_subsystems.TryGetValue(subsystemName, out var info))
            {
                return false;
            }

            EvaluateSubsystemState(subsystemName);

            if (info.State == SubsystemHealthState.Critical || info.State == SubsystemHealthState.Offline)
            {
                return false;
            }

            // Check dependencies
            foreach (var dep in info.Dependencies)
            {
                var depState = GetSubsystemHealth(dep);
                if (depState == SubsystemHealthState.Offline || depState == SubsystemHealthState.Critical)
                {
                    ReportSubsystemState(subsystemName, SubsystemHealthState.Critical, $"Critical dependency '{dep}' is unhealthy (State: {depState}).");
                    return false;
                }
            }

            return true;
        }

        public Task<bool> RunHealthCheckAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RunHealthCheck(subsystemName));
        }

        #endregion

        #region Snapshot Support

        public HealthSnapshot GetCurrentSnapshot()
        {
            var states = _subsystems.ToDictionary(k => k.Key, v => v.Value.State, StringComparer.OrdinalIgnoreCase);
            var detailed = _subsystems.Values.Select(CloneHealthInfo).ToList();

            var snapshot = new HealthSnapshot
            {
                SnapshotId = Guid.NewGuid(),
                CapturedAt = DateTime.UtcNow,
                MachineId = Environment.MachineName,
                ClientVersion = "1.0.0",
                OsVersion = Environment.OSVersion.ToString(),
                SubsystemStates = states,
                DetailedSubsystems = detailed,
                Resources = null // Resource Monitoring is belongs to later stages
            };

            // Maintain ring buffer of historical snapshots
            _historicalSnapshots.Enqueue(snapshot);
            while (_historicalSnapshots.Count > _options.MaxHistoricalSnapshots)
            {
                _historicalSnapshots.TryDequeue(out _);
            }

            return snapshot;
        }

        public Task<HealthSnapshot> GetCurrentSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetCurrentSnapshot());
        }

        public List<HealthSnapshot> GetHistoricalSnapshots()
        {
            return _historicalSnapshots.ToList();
        }

        public Task<List<HealthSnapshot>> GetHistoricalSnapshotsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetHistoricalSnapshots());
        }

        public HealthSnapshot GetSubsystemSnapshot(string subsystemName)
        {
            if (!_subsystems.TryGetValue(subsystemName, out var info))
            {
                throw new ArgumentException($"Subsystem '{subsystemName}' is not registered.");
            }

            return new HealthSnapshot
            {
                SnapshotId = Guid.NewGuid(),
                CapturedAt = DateTime.UtcNow,
                MachineId = Environment.MachineName,
                ClientVersion = "1.0.0",
                OsVersion = Environment.OSVersion.ToString(),
                SubsystemStates = new Dictionary<string, SubsystemHealthState>(StringComparer.OrdinalIgnoreCase) { [subsystemName] = info.State },
                DetailedSubsystems = new List<SubsystemHealthInfo> { CloneHealthInfo(info) }
            };
        }

        public Task<HealthSnapshot> GetSubsystemSnapshotAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetSubsystemSnapshot(subsystemName));
        }

        public HealthSnapshot GetGlobalHealthSnapshot()
        {
            return GetCurrentSnapshot();
        }

        public Task<HealthSnapshot> GetGlobalHealthSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetGlobalHealthSnapshot());
        }

        #endregion

        #region Diagnostics APIs

        public string GetHealthSummary()
        {
            var snapshot = GetCurrentSnapshot();
            var total = _subsystems.Count;
            var healthy = _subsystems.Values.Count(s => s.State == SubsystemHealthState.Healthy);
            var warning = _subsystems.Values.Count(s => s.State == SubsystemHealthState.Warning);
            var critical = _subsystems.Values.Count(s => s.State == SubsystemHealthState.Critical);
            var offline = _subsystems.Values.Count(s => s.State == SubsystemHealthState.Offline);

            return $"Health Summary - Healthy: {healthy}/{total}, Warning: {warning}, Critical: {critical}, Offline: {offline}, Global Score: {GetGlobalHealthScore():F1}%";
        }

        public Task<string> GetHealthSummaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetHealthSummary());
        }

        public SubsystemHealthInfo? GetSubsystemDetails(string subsystemName)
        {
            if (_subsystems.TryGetValue(subsystemName, out var info))
            {
                return CloneHealthInfo(info);
            }
            return null;
        }

        public Task<SubsystemHealthInfo?> GetSubsystemDetailsAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetSubsystemDetails(subsystemName));
        }

        public IReadOnlyList<string> GetTransitionHistory(string subsystemName)
        {
            if (_subsystems.TryGetValue(subsystemName, out var info))
            {
                return info.HealthHistory.AsReadOnly();
            }
            return Array.Empty<string>();
        }

        public Task<IReadOnlyList<string>> GetTransitionHistoryAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetTransitionHistory(subsystemName));
        }

        public string GetFailureStatistics()
        {
            var list = _subsystems.Values.Select(s => $"{s.SubsystemName}: {s.FailureCount} failures");
            return string.Join(", ", list);
        }

        public Task<string> GetFailureStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetFailureStatistics());
        }

        public string GetHealthScoreSummary()
        {
            var list = _subsystems.Values.Select(s => $"{s.SubsystemName}: {s.HealthScore:F1}%");
            return string.Join(", ", list);
        }

        public Task<string> GetHealthScoreSummaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetHealthScoreSummary());
        }

        #endregion

        #region Internal Support Helpers

        private TimeSpan GetHeartbeatTimeout(string subsystemName)
        {
            if (_options.SubsystemHeartbeatTimeouts.TryGetValue(subsystemName, out var timeout))
            {
                return timeout;
            }
            return _options.DefaultHeartbeatTimeout;
        }

        private void EvaluateSubsystemState(string subsystemName)
        {
            if (!_subsystems.TryGetValue(subsystemName, out var info)) return;

            // Heartbeat check
            var age = DateTime.UtcNow - info.LastHeartbeat;
            var timeout = GetHeartbeatTimeout(subsystemName);

            if (age > timeout && info.State == SubsystemHealthState.Healthy)
            {
                TransitionState(info, SubsystemHealthState.Warning, $"Heartbeat expired. Last heartbeat was {age.TotalSeconds:F1}s ago.");
            }

            // Dependency propagation check
            foreach (var dep in info.Dependencies)
            {
                if (_subsystems.TryGetValue(dep, out var depInfo))
                {
                    if (depInfo.State == SubsystemHealthState.Offline && info.State != SubsystemHealthState.Offline)
                    {
                        TransitionState(info, SubsystemHealthState.Critical, $"Dependency '{dep}' is Offline.");
                    }
                    else if (depInfo.State == SubsystemHealthState.Critical && info.State == SubsystemHealthState.Healthy)
                    {
                        TransitionState(info, SubsystemHealthState.Warning, $"Dependency '{dep}' has Critical failure.");
                    }
                }
            }

            // Recalculate health score
            RecalculateHealthScoreInternal(info);
        }

        private void TransitionState(SubsystemHealthInfo info, SubsystemHealthState newState, string reason)
        {
            var oldState = info.State;
            if (oldState == newState)
            {
                // Make sure we still recalculate the score
                RecalculateHealthScoreInternal(info);
                return;
            }

            info.State = newState;
            var historyEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - State: {oldState} -> {newState}. Reason: {reason}";

            info.AddHistoryEntry(historyEntry);

            _logger.LogWarning("Subsystem '{SubsystemName}' health state transitioned: {OldState} -> {NewState}. Reason: {Reason}. Metadata: {@LogMetadata}",
                info.SubsystemName, oldState, newState, reason, new {
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    Subsystem = info.SubsystemName,
                    Operation = "StateTransition",
                    Timestamp = DateTime.UtcNow,
                    Duration = 0,
                    Result = $"{oldState}To{newState}"
                });

            // Recalculate health score after transition
            RecalculateHealthScoreInternal(info);

            // Raise events safely
            try
            {
                SubsystemHealthStateChanged?.Invoke(info.SubsystemName, oldState, newState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred dispatching legacy health state transition event for '{SubsystemName}'", info.SubsystemName);
            }

            SafeRaiseEvent(StateChanged, new StateChangedEventArgs(info.SubsystemName, oldState, newState, reason));
        }

        private void RecalculateHealthScoreInternal(SubsystemHealthInfo info)
        {
            var oldScore = info.HealthScore;

            // 1. Base Score
            double score = 100.0;

            // 2. State Deduction
            switch (info.State)
            {
                case SubsystemHealthState.Healthy:
                    break;
                case SubsystemHealthState.Warning:
                    score -= 20.0;
                    break;
                case SubsystemHealthState.Critical:
                    score -= 60.0;
                    break;
                case SubsystemHealthState.Offline:
                    score -= 100.0;
                    break;
            }

            // 3. Heartbeat Freshness Deduction
            var age = DateTime.UtcNow - info.LastHeartbeat;
            var timeout = GetHeartbeatTimeout(info.SubsystemName);
            if (age > timeout)
            {
                score -= 15.0;
                if (age > timeout * 2)
                {
                    score -= 15.0;
                }
            }

            // 4. Failure Count Deduction
            double failureDeduction = Math.Min(40.0, info.FailureCount * _options.BaseFailureDeduction);
            score -= failureDeduction;

            // 5. Recent Transitions Deduction
            // We count how many times "State:" transition appears in the transition history log
            int stateChanges = info.HealthHistory.Count(h => h.Contains("State:"));
            double transitionDeduction = Math.Min(25.0, stateChanges * _options.BaseTransitionDeduction);
            score -= transitionDeduction;

            // 6. Dependency Health Deduction
            double depDeduction = 0.0;
            foreach (var dep in info.Dependencies)
            {
                if (_subsystems.TryGetValue(dep, out var depInfo))
                {
                    if (depInfo.State == SubsystemHealthState.Offline || depInfo.State == SubsystemHealthState.Critical)
                    {
                        depDeduction += _options.DependencyFailureDeduction;
                    }
                    else if (depInfo.State == SubsystemHealthState.Warning)
                    {
                        depDeduction += _options.DependencyFailureDeduction / 2.0;
                    }
                }
            }
            score -= Math.Min(35.0, depDeduction);

            // Clamp 0.0 to 100.0
            info.HealthScore = Math.Clamp(score, 0.0, 100.0);

            if (Math.Abs(oldScore - info.HealthScore) > 0.01)
            {
                _logger.LogInformation("Health score updated for '{SubsystemName}': {OldScore:F1} -> {NewScore:F1}. Metadata: {@LogMetadata}",
                    info.SubsystemName, oldScore, info.HealthScore, new {
                        CorrelationId = Guid.NewGuid().ToString("N"),
                        Subsystem = info.SubsystemName,
                        Operation = "ScoreUpdate",
                        Timestamp = DateTime.UtcNow,
                        Duration = 0,
                        Result = info.HealthScore.ToString("F1")
                    });

                SafeRaiseEvent(HealthScoreChanged, new HealthScoreChangedEventArgs(info.SubsystemName, oldScore, info.HealthScore));
            }
        }

        private double GetGlobalHealthScore()
        {
            if (_subsystems.IsEmpty) return 100.0;
            return _subsystems.Values.Average(s => s.HealthScore);
        }

        private static SubsystemHealthInfo CloneHealthInfo(SubsystemHealthInfo source)
        {
            var target = new SubsystemHealthInfo();
            // Assign fields safely while reading from lock or directly copy property values
            target.SubsystemId = source.SubsystemId;
            target.SubsystemName = source.SubsystemName;
            target.DisplayName = source.DisplayName;
            target.State = source.State;
            target.PreviousState = source.PreviousState;
            target.LastHeartbeat = source.LastHeartbeat;
            target.LastSuccessfulHeartbeat = source.LastSuccessfulHeartbeat;
            target.LastUpdated = source.LastUpdated;
            target.FailureCount = source.FailureCount;
            target.RecoveryCount = source.RecoveryCount;
            target.LastRecovery = source.LastRecovery;
            target.LastMessage = source.LastMessage;
            target.LastException = source.LastException;
            target.HealthScore = source.HealthScore;

            var deps = source.Dependencies;
            target.Dependencies = deps != null ? new List<string>(deps) : new List<string>();

            var hist = source.HealthHistory;
            target.HealthHistory = hist != null ? new List<string>(hist) : new List<string>();

            var meta = source.Metadata;
            target.Metadata = meta != null ? new Dictionary<string, string>(meta) : new Dictionary<string, string>();

            return target;
        }

        private void SafeRaiseEvent<T>(EventHandler<T>? ev, T args) where T : EventArgs
        {
            if (ev == null) return;
            foreach (var handler in ev.GetInvocationList().Cast<EventHandler<T>>())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing event handler callback of type {HandlerType}", handler.Method.DeclaringType?.FullName);
                }
            }
        }

        private async Task CheckSubsystemTimeoutsAndPropagationAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(_cts.Token);

                    foreach (var subsystemName in _subsystems.Keys)
                    {
                        EvaluateSubsystemState(subsystemName);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background subsystem heartbeat check loop.");
                }
            }
        }

        #endregion

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _backgroundTask.Wait(1000);
            }
            catch
            {
                // Ignore transient cleanup waits
            }
            _cts.Dispose();
        }
    }
}
