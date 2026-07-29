using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sayra.Client.Shared.Models.Recovery;

namespace SayraClient.Services.Recovery
{
    public class RecoveryMetricsCollector
    {
        private readonly object _lock = new();
        private int _recoveryCount;
        private int _successCount;
        private int _failureCount;
        private int _retryCount;
        private int _escalationCount;
        private int _activeRecoveries;
        private TimeSpan _totalRecoveryTime = TimeSpan.Zero;
        private TimeSpan _longestRecovery = TimeSpan.Zero;

        private readonly ConcurrentDictionary<string, RecoveryHistory> _history = new(StringComparer.OrdinalIgnoreCase);

        public int RecoveryCount => _recoveryCount;
        public int SuccessCount => _successCount;
        public int FailureCount => _failureCount;
        public int RetryCount => _retryCount;
        public int EscalationCount => _escalationCount;
        public int ActiveRecoveries => _activeRecoveries;

        public double SuccessRate
        {
            get
            {
                lock (_lock)
                {
                    if (_recoveryCount == 0) return 100.0;
                    return (_successCount * 100.0) / _recoveryCount;
                }
            }
        }

        public double FailureRate
        {
            get
            {
                lock (_lock)
                {
                    if (_recoveryCount == 0) return 0.0;
                    return (_failureCount * 100.0) / _recoveryCount;
                }
            }
        }

        public TimeSpan AverageRecoveryTime
        {
            get
            {
                lock (_lock)
                {
                    if (_successCount == 0) return TimeSpan.Zero;
                    return TimeSpan.FromTicks(_totalRecoveryTime.Ticks / _successCount);
                }
            }
        }

        public TimeSpan LongestRecovery => _longestRecovery;

        public void IncrementActiveRecoveries()
        {
            lock (_lock)
            {
                _activeRecoveries++;
            }
        }

        public void DecrementActiveRecoveries()
        {
            lock (_lock)
            {
                if (_activeRecoveries > 0) _activeRecoveries--;
            }
        }

        public void IncrementRetries()
        {
            lock (_lock)
            {
                _retryCount++;
            }
        }

        public void IncrementEscalations()
        {
            lock (_lock)
            {
                _escalationCount++;
            }
        }

        public void RecordRecoveryAttempt(string subsystem, string action, int attemptNumber)
        {
            lock (_lock)
            {
                _recoveryCount++;
            }

            var hist = _history.GetOrAdd(subsystem, s => new RecoveryHistory { SubsystemName = s });
            lock (hist)
            {
                hist.Failures.Add(new FailureRecord
                {
                    SubsystemName = subsystem,
                    ErrorMessage = $"Attempt {attemptNumber} with action {action}",
                    DetectedAt = DateTime.UtcNow
                });
            }
        }

        public void RecordRecoveryResult(string subsystem, Guid attemptId, bool success, TimeSpan duration, string message, string? error)
        {
            lock (_lock)
            {
                if (success)
                {
                    _successCount++;
                    _totalRecoveryTime += duration;
                    if (duration > _longestRecovery)
                    {
                        _longestRecovery = duration;
                    }
                }
                else
                {
                    _failureCount++;
                }
            }

            var hist = _history.GetOrAdd(subsystem, s => new RecoveryHistory { SubsystemName = s });
            lock (hist)
            {
                if (success) hist.TotalSuccessfulRecoveries++;
                hist.TotalFailures = hist.Failures.Count;

                hist.RecoveryResults.Add(new RecoveryResult
                {
                    AttemptId = attemptId,
                    SubsystemName = subsystem,
                    IsSuccessful = success,
                    FinalStatus = success ? RecoveryStatus.Success : RecoveryStatus.Failed,
                    CompletedAt = DateTime.UtcNow,
                    Duration = duration,
                    OutputMessage = message,
                    ErrorDetails = error
                });
            }
        }

        public RecoveryHistory? GetHistory(string subsystem)
        {
            return _history.TryGetValue(subsystem, out var hist) ? hist : null;
        }

        public List<RecoveryHistory> GetAllHistory()
        {
            return _history.Values.ToList();
        }
    }
}
