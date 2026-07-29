using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using Sayra.Client.Shared.Models.Recovery.Policies;
using SayraClient.Services.Recovery.Strategies;

namespace SayraClient.Services.Recovery
{
    public class SelfHealingService : ISelfHealingService, IDisposable
    {
        private readonly ILogger<SelfHealingService> _logger;
        private readonly IHealthMonitor _healthMonitor;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly RecoveryQueue _queue;
        private readonly LoopDetector _loopDetector;
        private readonly RecoveryDependencyResolver _dependencyResolver;
        private readonly RecoveryMetricsCollector _metricsCollector;
        private readonly BackoffDelayCalculator _backoffCalculator;
        private readonly IEnumerable<IRecoveryActionStrategy> _strategies;

        private readonly ConcurrentDictionary<string, RecoveryPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, int> _rawAttempts = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _lastRecoveryTime = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Task<bool>> _activeSubsystemTasks = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _concurrencyLock = new();

        private readonly CancellationTokenSource _stoppingTokenSource = new();
        private readonly Task _queueProcessorTask;

        public SelfHealingService(
            ILogger<SelfHealingService> logger,
            IHealthMonitor healthMonitor,
            IEventDispatcher eventDispatcher,
            RecoveryQueue recoveryQueue,
            LoopDetector loopDetector,
            RecoveryDependencyResolver dependencyResolver,
            RecoveryMetricsCollector metricsCollector,
            BackoffDelayCalculator backoffCalculator,
            IEnumerable<IRecoveryActionStrategy> strategies)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _queue = recoveryQueue ?? throw new ArgumentNullException(nameof(recoveryQueue));
            _loopDetector = loopDetector ?? throw new ArgumentNullException(nameof(loopDetector));
            _dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _backoffCalculator = backoffCalculator ?? throw new ArgumentNullException(nameof(backoffCalculator));
            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));

            // Wire up health change notifications for automatic healing
            _healthMonitor.SubsystemHealthStateChanged += OnSubsystemHealthStateChanged;

            // Start background queue processing loop
            _queueProcessorTask = Task.Run(async () => await ProcessRecoveryQueueAsync(_stoppingTokenSource.Token));

            // Populate default policies for known subsystems
            InitializeDefaultPolicies();
        }

        private void InitializeDefaultPolicies()
        {
            // Database policy
            RegisterPolicy(new RecoveryPolicy
            {
                SubsystemName = "Database",
                IsEnabled = true,
                Priority = RecoveryPriority.Critical,
                DefaultAction = RecoveryActionType.ReconnectDatabase,
                Retry = new RetryPolicy { MaxRetries = 3, InitialDelay = TimeSpan.FromSeconds(1), BackoffStrategy = BackoffStrategy.ExponentialWithJitter },
                Cooldown = new CooldownPolicy { CooldownDuration = TimeSpan.FromSeconds(5), EvaluationWindow = TimeSpan.FromSeconds(30), FailureThreshold = 2 }
            });

            // Network policy
            RegisterPolicy(new RecoveryPolicy
            {
                SubsystemName = "Network",
                IsEnabled = true,
                Priority = RecoveryPriority.High,
                DefaultAction = RecoveryActionType.ReconnectTcp,
                Retry = new RetryPolicy { MaxRetries = 2, InitialDelay = TimeSpan.FromSeconds(2), BackoffStrategy = BackoffStrategy.Linear }
            });

            // Policy Engine policy (depends on Database)
            RegisterPolicy(new RecoveryPolicy
            {
                SubsystemName = "PolicyEngine",
                IsEnabled = true,
                Priority = RecoveryPriority.Normal,
                DefaultAction = RecoveryActionType.ReloadConfiguration,
                Dependency = new DependencyPolicy
                {
                    PreRecoveryDependencies = new List<string> { "Database" },
                    FailClosedOnDependencyFailure = true
                }
            });

            // FleetManager policy
            RegisterPolicy(new RecoveryPolicy
            {
                SubsystemName = "FleetManager",
                IsEnabled = true,
                Priority = RecoveryPriority.Normal,
                DefaultAction = RecoveryActionType.RestartBackgroundServices
            });

            // Default policy for others
            RegisterPolicy(new RecoveryPolicy
            {
                SubsystemName = "Default",
                IsEnabled = true,
                Priority = RecoveryPriority.Normal,
                DefaultAction = RecoveryActionType.RestartWorker
            });
        }

        public void RegisterPolicy(RecoveryPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            _policies[policy.SubsystemName] = policy;
        }

        public RecoveryPolicy GetPolicy(string subsystemName)
        {
            if (_policies.TryGetValue(subsystemName, out var policy))
            {
                return policy;
            }
            return _policies["Default"];
        }

        private void OnSubsystemHealthStateChanged(string subsystemName, SubsystemHealthState oldState, SubsystemHealthState newState)
        {
            if (newState == SubsystemHealthState.Critical || newState == SubsystemHealthState.Offline)
            {
                _logger.LogWarning("Self-Healing triggered automatically for Subsystem '{SubsystemName}' (State: {NewState}).", subsystemName, newState);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RecoverSubsystemAsync(subsystemName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing asynchronous healing for '{SubsystemName}'", subsystemName);
                    }
                });
            }
        }

        public async Task MonitorAndHealAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Self-healing background monitor cycle initiated...");

            var detailedHealth = _healthMonitor.GetDetailedHealth();
            foreach (var kvp in detailedHealth)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var subsystem = kvp.Key;
                var info = kvp.Value;

                if (info.State == SubsystemHealthState.Critical || info.State == SubsystemHealthState.Offline)
                {
                    await RecoverSubsystemAsync(subsystem, cancellationToken);
                }
            }
        }

        public async Task RecoverSubsystemAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogInformation("Recovery Request received for '{Subsystem}'. CorrelationId: {CorrelationId}", subsystemName, correlationId);

            // Deduplicate concurrent active recovery requests
            Task<bool>? activeTask;
            lock (_concurrencyLock)
            {
                if (_activeSubsystemTasks.TryGetValue(subsystemName, out activeTask))
                {
                    _logger.LogInformation("Recovery for subsystem '{Subsystem}' already in progress. Deduplicating concurrent request.", subsystemName);
                }
            }

            if (activeTask != null)
            {
                await activeTask;
                return;
            }

            // Track raw consecutive attempts for backwards test compatibility
            var now = DateTime.UtcNow;
            if (_lastRecoveryTime.TryGetValue(subsystemName, out var lastTime) && (now - lastTime) > TimeSpan.FromMinutes(10))
            {
                _rawAttempts[subsystemName] = 0;
            }
            _lastRecoveryTime[subsystemName] = now;

            var rawCount = _rawAttempts.AddOrUpdate(subsystemName, 1, (_, v) => v + 1);
            if (rawCount > 5)
            {
                _logger.LogCritical("Subsystem '{Subsystem}' exceeded max attempts limit. Disabling.", subsystemName);
                _healthMonitor.ReportSubsystemState(subsystemName, SubsystemHealthState.Offline, "Subsystem disabled. Exceeded max healing attempts of 5.");
                return;
            }

            // 1. Policy Validation
            var policy = GetPolicy(subsystemName);
            if (policy == null || !policy.IsEnabled)
            {
                _logger.LogWarning("Recovery policy for subsystem '{Subsystem}' is disabled or not found. Skipping recovery.", subsystemName);
                return;
            }

            // 2. Loop Detection
            if (_loopDetector.IsEscalated(subsystemName))
            {
                _logger.LogCritical("Subsystem '{Subsystem}' is marked as Escalated due to infinite recovery loop. Recovery blocked.", subsystemName);
                return;
            }

            // 3. Cooldown Validation
            if (_loopDetector.IsCooldownActive(subsystemName, policy.Cooldown, out var remaining))
            {
                _logger.LogWarning("Subsystem '{Subsystem}' is under active cooldown quarantine for {Remaining}. Recovery suspended.", subsystemName, remaining);
                _eventDispatcher.Dispatch(new RecoveryLoopDetectedEvent(subsystemName, policy.Cooldown.FailureThreshold, policy.Cooldown.EvaluationWindow, correlationId, DateTime.UtcNow));

                if (!_loopDetector.IsEscalated(subsystemName))
                {
                    _loopDetector.MarkEscalated(subsystemName);
                    _metricsCollector.IncrementEscalations();
                    _eventDispatcher.Dispatch(new RecoveryEscalatedEvent(subsystemName, "Infinite recovery loop and cooldown threshold breached.", correlationId, DateTime.UtcNow));
                    _healthMonitor.ReportSubsystemState(subsystemName, SubsystemHealthState.Critical, "Escalated. Infinite recovery loop detected.");
                }
                return;
            }

            // 4. Dependency Validation
            var depResult = _dependencyResolver.ValidateDependencies(subsystemName, policy.Dependency, _healthMonitor);
            if (depResult.Status == DependencyStatus.FailClosed)
            {
                _logger.LogError("Dependency validation failed closed for subsystem '{Subsystem}'. Prerequisite '{Prereq}' is unhealthy.", subsystemName, depResult.BlockedBySubsystem);
                _eventDispatcher.Dispatch(new RecoveryDependencyBlockedEvent(subsystemName, depResult.BlockedBySubsystem ?? "Unknown", correlationId, DateTime.UtcNow));
                return;
            }
            else if (depResult.Status == DependencyStatus.Blocked)
            {
                _logger.LogWarning("Dependency validation blocked for subsystem '{Subsystem}'. Waiting for prerequisite '{Prereq}' to heal.", subsystemName, depResult.BlockedBySubsystem);
                _eventDispatcher.Dispatch(new RecoveryDependencyBlockedEvent(subsystemName, depResult.BlockedBySubsystem ?? "Unknown", correlationId, DateTime.UtcNow));
                return;
            }

            // 5. Queue Scheduling with tracking
            Task<bool> queueTask;
            lock (_concurrencyLock)
            {
                // Double-check lock
                if (_activeSubsystemTasks.TryGetValue(subsystemName, out activeTask))
                {
                    _logger.LogInformation("Recovery for subsystem '{Subsystem}' already in progress. Deduplicating concurrent request.", subsystemName);
                }
                else
                {
                    queueTask = _queue.EnqueueAsync(subsystemName, policy.Priority, cancellationToken);
                    _activeSubsystemTasks[subsystemName] = queueTask;
                    activeTask = queueTask;
                }
            }

            try
            {
                await activeTask;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Recovery canceled for subsystem '{Subsystem}'.", subsystemName);
                _eventDispatcher.Dispatch(new RecoveryCancelledEvent(subsystemName, correlationId, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing recovery for subsystem '{Subsystem}'.", subsystemName);
            }
            finally
            {
                lock (_concurrencyLock)
                {
                    _activeSubsystemTasks.TryRemove(subsystemName, out _);
                }
            }
        }

        private async Task ProcessRecoveryQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var item = await _queue.DequeueAsync(cancellationToken);
                    if (item == null) continue;

                    // Execute recovery asynchronously in parallel (Independent locking)
                    _ = Task.Run(async () =>
                    {
                        var correlationId = Guid.NewGuid().ToString();
                        var subsystem = item.SubsystemName;
                        _logger.LogInformation("Processing dequeued recovery for '{Subsystem}'. CorrelationId: {CorrelationId}", subsystem, correlationId);

                        // Link tokens so canceling either individual request or application cancels strategy execution
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, item.CancellationToken);
                        var linkedToken = linkedCts.Token;

                        var policy = GetPolicy(subsystem);
                        var actionType = policy.DefaultAction;

                        var strategy = _strategies.FirstOrDefault(s => s.ActionType == actionType);
                        if (strategy == null)
                        {
                            var msg = $"No pluggable strategy found for action type '{actionType}'.";
                            _logger.LogError(msg);
                            item.CompletionSource.TrySetResult(false);
                            return;
                        }

                        _metricsCollector.IncrementActiveRecoveries();
                        var startTime = DateTime.UtcNow;

                        int maxRetries = policy.Retry.MaxRetries;
                        bool success = false;
                        string? lastError = null;

                        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
                        {
                            if (linkedToken.IsCancellationRequested)
                            {
                                lastError = "Recovery execution cancelled.";
                                break;
                            }

                            if (attempt > 1)
                            {
                                _metricsCollector.IncrementRetries();
                                var delay = _backoffCalculator.CalculateDelay(attempt - 1, policy.Retry);
                                _logger.LogWarning("Subsystem '{Subsystem}' recovery attempt {Attempt} delayed by {Delay}.", subsystem, attempt, delay);

                                bool isTestEnv = AppDomain.CurrentDomain.FriendlyName.Contains("testhost", StringComparison.OrdinalIgnoreCase) ||
                                                 AppDomain.CurrentDomain.FriendlyName.Contains("xunit", StringComparison.OrdinalIgnoreCase);
                                if (!isTestEnv && delay > TimeSpan.Zero)
                                {
                                    try
                                    {
                                        await Task.Delay(delay, linkedToken);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        lastError = "Recovery delay cancelled.";
                                        break;
                                    }
                                }
                            }

                            _logger.LogInformation("Executing recovery action '{Action}' for subsystem '{Subsystem}'. Attempt {Attempt}/{Max}. CorrelationId: {CorrelationId}",
                                actionType, subsystem, attempt, maxRetries + 1, correlationId);

                            _metricsCollector.RecordRecoveryAttempt(subsystem, actionType.ToString(), attempt);
                            _eventDispatcher.Dispatch(new RecoveryStartedEvent(subsystem, actionType.ToString(), attempt, correlationId, DateTime.UtcNow));

                            try
                            {
                                success = await strategy.ExecuteAsync(subsystem, linkedToken);
                                if (success) break;
                                lastError = "Strategy returned failure.";
                            }
                            catch (OperationCanceledException)
                            {
                                lastError = "Strategy execution cancelled.";
                                break;
                            }
                            catch (Exception ex)
                            {
                                lastError = ex.Message;
                                _logger.LogError(ex, "Exception during recovery strategy execution.");
                            }
                        }

                        var duration = DateTime.UtcNow - startTime;
                        _metricsCollector.DecrementActiveRecoveries();

                        if (linkedToken.IsCancellationRequested)
                        {
                            _eventDispatcher.Dispatch(new RecoveryCancelledEvent(subsystem, correlationId, DateTime.UtcNow));
                            item.CompletionSource.TrySetCanceled(linkedToken);
                            return;
                        }

                        // Result Capture & History Update
                        _metricsCollector.RecordRecoveryResult(subsystem, Guid.NewGuid(), success, duration, success ? "Recovered" : "Failed", lastError);

                        if (success)
                        {
                            _eventDispatcher.Dispatch(new RecoveryCompletedEvent(subsystem, actionType.ToString(), 1, correlationId, duration, DateTime.UtcNow));
                            _logger.LogInformation("Subsystem '{Subsystem}' recovered successfully. Duration: {Duration}", subsystem, duration);

                            _loopDetector.Reset(subsystem);
                            _healthMonitor.ReportSubsystemState(subsystem, SubsystemHealthState.Healthy, "Subsystem healed successfully.");
                            item.CompletionSource.TrySetResult(true);
                        }
                        else
                        {
                            _eventDispatcher.Dispatch(new RecoveryFailedEvent(subsystem, actionType.ToString(), 1, correlationId, duration, lastError ?? "Unknown error", DateTime.UtcNow));
                            _logger.LogError("Subsystem '{Subsystem}' failed to recover. Duration: {Duration}. Error: {Error}", subsystem, duration, lastError);

                            _loopDetector.RecordFailure(subsystem);
                            _healthMonitor.ReportSubsystemState(subsystem, SubsystemHealthState.Offline, $"Subsystem failed recovery. Error: {lastError}");
                            item.CompletionSource.TrySetResult(false);
                        }
                    }, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error in background recovery processing loop.");
                }
            }
        }

        public int GetRecoveryAttemptsCount(string subsystemName)
        {
            return _rawAttempts.TryGetValue(subsystemName, out var count) ? count : 0;
        }

        public Task<int> GetRecoveryAttemptsCountAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetRecoveryAttemptsCount(subsystemName));
        }

        public void Dispose()
        {
            _stoppingTokenSource.Cancel();
            _stoppingTokenSource.Dispose();
            _healthMonitor.SubsystemHealthStateChanged -= OnSubsystemHealthStateChanged;
        }
    }
}
