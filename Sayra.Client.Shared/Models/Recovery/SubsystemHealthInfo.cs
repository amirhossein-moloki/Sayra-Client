using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents comprehensive health and operational metadata for a managed subsystem.
    /// This model is designed to be thread-safe for property operations and state transitions.
    /// </summary>
    public class SubsystemHealthInfo
    {
        private readonly object _lock = new();
        private string _subsystemName = string.Empty;
        private SubsystemHealthState _state = SubsystemHealthState.Healthy;
        private SubsystemHealthState _previousState = SubsystemHealthState.Healthy;
        private DateTime _lastHeartbeat = DateTime.UtcNow;
        private int _failureCount;
        private int _recoveryCount;
        private double _healthScore = 100.0;
        private DateTime? _lastRecovery;
        private string _lastMessage = string.Empty;
        private string? _lastException;
        private List<string> _dependencies = new();
        private List<string> _healthHistory = new();
        private ConcurrentDictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the unique name or identifier of the subsystem.
        /// </summary>
        public string SubsystemName
        {
            get { lock (_lock) return _subsystemName; }
            set { lock (_lock) _subsystemName = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the current health state of the subsystem.
        /// </summary>
        public SubsystemHealthState State
        {
            get { lock (_lock) return _state; }
            set
            {
                lock (_lock)
                {
                    if (_state != value)
                    {
                        _previousState = _state;
                        _state = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the previous health state before the last transition.
        /// </summary>
        public SubsystemHealthState PreviousState
        {
            get { lock (_lock) return _previousState; }
            set { lock (_lock) _previousState = value; }
        }

        /// <summary>
        /// Gets or sets the timestamp of the last recorded heartbeat.
        /// </summary>
        public DateTime LastHeartbeat
        {
            get { lock (_lock) return _lastHeartbeat; }
            set { lock (_lock) _lastHeartbeat = value; }
        }

        /// <summary>
        /// Gets or sets the number of failures recorded for this subsystem.
        /// </summary>
        public int FailureCount
        {
            get { lock (_lock) return _failureCount; }
            set { lock (_lock) _failureCount = value; }
        }

        /// <summary>
        /// Gets or sets the number of successful recovery/healing attempts performed on this subsystem.
        /// </summary>
        public int RecoveryCount
        {
            get { lock (_lock) return _recoveryCount; }
            set { lock (_lock) _recoveryCount = value; }
        }

        /// <summary>
        /// Gets or sets the list of subsystem dependencies.
        /// </summary>
        public List<string> Dependencies
        {
            get { lock (_lock) return _dependencies; }
            set { lock (_lock) _dependencies = value ?? new List<string>(); }
        }

        /// <summary>
        /// Gets or sets the transition history log of health state changes.
        /// </summary>
        public List<string> HealthHistory
        {
            get { lock (_lock) return _healthHistory; }
            set { lock (_lock) _healthHistory = value ?? new List<string>(); }
        }

        /// <summary>
        /// Gets or sets the last diagnostic message reported by this subsystem.
        /// </summary>
        public string LastMessage
        {
            get { lock (_lock) return _lastMessage; }
            set { lock (_lock) _lastMessage = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the last recorded exception details, if any.
        /// </summary>
        public string? LastException
        {
            get { lock (_lock) return _lastException; }
            set { lock (_lock) _lastException = value; }
        }

        /// <summary>
        /// Gets or sets the overall health score (0.0 to 100.0) of this subsystem.
        /// </summary>
        public double HealthScore
        {
            get { lock (_lock) return _healthScore; }
            set { lock (_lock) _healthScore = value; }
        }

        /// <summary>
        /// Gets or sets the timestamp of the last executed recovery operation.
        /// </summary>
        public DateTime? LastRecovery
        {
            get { lock (_lock) return _lastRecovery; }
            set { lock (_lock) _lastRecovery = value; }
        }

        /// <summary>
        /// Gets or sets custom metadata associated with this subsystem's operations.
        /// </summary>
        public Dictionary<string, string> Metadata
        {
            get
            {
                lock (_lock)
                {
                    return new Dictionary<string, string>(_metadata, StringComparer.OrdinalIgnoreCase);
                }
            }
            set
            {
                lock (_lock)
                {
                    _metadata = value != null
                        ? new ConcurrentDictionary<string, string>(value, StringComparer.OrdinalIgnoreCase)
                        : new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>
        /// Adds a transition history entry to the log in a thread-safe manner, keeping the log capped at 50 entries.
        /// </summary>
        /// <param name="entry">The history log entry message.</param>
        public void AddHistoryEntry(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return;
            lock (_lock)
            {
                if (_healthHistory.Count >= 50)
                {
                    _healthHistory.RemoveAt(0);
                }
                _healthHistory.Add(entry);
            }
        }

        /// <summary>
        /// Safely sets metadata key-value pair.
        /// </summary>
        public void SetMetadata(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _metadata[key] = value ?? string.Empty;
        }

        /// <summary>
        /// Safely gets metadata value by key.
        /// </summary>
        public string? GetMetadata(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _metadata.TryGetValue(key, out var val) ? val : null;
        }
    }
}
