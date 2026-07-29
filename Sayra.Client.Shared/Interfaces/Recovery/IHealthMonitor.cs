using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    #region Strongly Typed Event Arguments

    public class SubsystemEventArgs : EventArgs
    {
        public string SubsystemName { get; }
        public SubsystemEventArgs(string subsystemName) => SubsystemName = subsystemName;
    }

    public class SubsystemRegisteredEventArgs : SubsystemEventArgs
    {
        public List<string> Dependencies { get; }
        public SubsystemRegisteredEventArgs(string name, List<string> dependencies) : base(name) => Dependencies = dependencies;
    }

    public class HeartbeatUpdatedEventArgs : SubsystemEventArgs
    {
        public DateTime Timestamp { get; }
        public HeartbeatUpdatedEventArgs(string name, DateTime timestamp) : base(name) => Timestamp = timestamp;
    }

    public class StateChangedEventArgs : SubsystemEventArgs
    {
        public SubsystemHealthState OldState { get; }
        public SubsystemHealthState NewState { get; }
        public string Message { get; }
        public StateChangedEventArgs(string name, SubsystemHealthState oldState, SubsystemHealthState newState, string message) : base(name)
        {
            OldState = oldState;
            NewState = newState;
            Message = message;
        }
    }

    public class FailureRecordedEventArgs : SubsystemEventArgs
    {
        public string ErrorMessage { get; }
        public string? ExceptionDetails { get; }
        public FailureRecordedEventArgs(string name, string errMsg, string? exDetails) : base(name)
        {
            ErrorMessage = errMsg;
            ExceptionDetails = exDetails;
        }
    }

    public class RecoveryCountUpdatedEventArgs : SubsystemEventArgs
    {
        public int NewCount { get; }
        public RecoveryCountUpdatedEventArgs(string name, int newCount) : base(name) => NewCount = newCount;
    }

    public class HealthScoreChangedEventArgs : SubsystemEventArgs
    {
        public double OldScore { get; }
        public double NewScore { get; }
        public HealthScoreChangedEventArgs(string name, double oldScore, double newScore) : base(name)
        {
            OldScore = oldScore;
            NewScore = newScore;
        }
    }

    #endregion

    /// <summary>
    /// Contract for monitoring the heartbeat, health transitions, and dependencies of all active subsystems.
    /// </summary>
    public interface IHealthMonitor
    {
        /// <summary>
        /// Event dispatched whenever a subsystem's health state transitions (e.g., Healthy -> Critical).
        /// </summary>
        event Action<string, SubsystemHealthState, SubsystemHealthState>? SubsystemHealthStateChanged;

        /// <summary>
        /// Raised when a new subsystem is registered under health tracking.
        /// </summary>
        event EventHandler<SubsystemRegisteredEventArgs>? SubsystemRegistered;

        /// <summary>
        /// Raised when a subsystem's heartbeat is updated.
        /// </summary>
        event EventHandler<HeartbeatUpdatedEventArgs>? HeartbeatUpdated;

        /// <summary>
        /// Raised when a subsystem's health state transitions.
        /// </summary>
        event EventHandler<StateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Raised when a subsystem failure is recorded.
        /// </summary>
        event EventHandler<FailureRecordedEventArgs>? FailureRecorded;

        /// <summary>
        /// Raised when a subsystem's recovery count is updated.
        /// </summary>
        event EventHandler<RecoveryCountUpdatedEventArgs>? RecoveryCountUpdated;

        /// <summary>
        /// Raised when a subsystem's health score changes.
        /// </summary>
        event EventHandler<HealthScoreChangedEventArgs>? HealthScoreChanged;

        /// <summary>
        /// Raised when a subsystem is unregistered/removed.
        /// </summary>
        event EventHandler<SubsystemEventArgs>? SubsystemRemoved;

        /// <summary>
        /// Registers a heartbeat ping from the specified subsystem to prevent timeout alerts.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        void ReportHeartbeat(string subsystemName);

        /// <summary>
        /// Registers a heartbeat ping asynchronously.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task ReportHeartbeatAsync(string subsystemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reports a status update or health transition for a given subsystem.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <param name="state">The reported health state.</param>
        /// <param name="message">A description or reason for the state.</param>
        /// <param name="exceptionDetails">Optional stack trace or exception trace details.</param>
        void ReportSubsystemState(string subsystemName, SubsystemHealthState state, string message, string? exceptionDetails = null);

        /// <summary>
        /// Reports a status update or health transition asynchronously.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <param name="state">The reported health state.</param>
        /// <param name="message">A description or reason for the state.</param>
        /// <param name="exceptionDetails">Optional exception details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task ReportSubsystemStateAsync(string subsystemName, SubsystemHealthState state, string message, string? exceptionDetails = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current health state of a subsystem.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <returns>The SubsystemHealthState.</returns>
        SubsystemHealthState GetSubsystemHealth(string subsystemName);

        /// <summary>
        /// Gets the current health state of a subsystem asynchronously.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the SubsystemHealthState.</returns>
        Task<SubsystemHealthState> GetSubsystemHealthAsync(string subsystemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed health metrics and state logs for all registered subsystems.
        /// </summary>
        /// <returns>A read-only dictionary of subsystem names and their health details.</returns>
        IReadOnlyDictionary<string, SubsystemHealthInfo> GetDetailedHealth();

        /// <summary>
        /// Gets detailed health metrics and state logs asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the health details dictionary.</returns>
        Task<IReadOnlyDictionary<string, SubsystemHealthInfo>> GetDetailedHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a quick diagnostic self-check on the specified subsystem and its dependencies.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <returns>True if the subsystem and all its dependencies are healthy, false otherwise.</returns>
        bool RunHealthCheck(string subsystemName);

        /// <summary>
        /// Runs a quick diagnostic check asynchronously.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning true if the subsystem check passes.</returns>
        Task<bool> RunHealthCheckAsync(string subsystemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Explicitly registers a subsystem under health tracking with its corresponding dependency list.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <param name="dependencies"> A list of subsystem names that this subsystem depends on.</param>
        void RegisterSubsystem(string subsystemName, List<string> dependencies);

        /// <summary>
        /// Registers a subsystem asynchronously.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <param name="dependencies">A list of subsystem dependencies.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task RegisterSubsystemAsync(string subsystemName, List<string> dependencies, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unregisters a subsystem from health tracking.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem to unregister.</param>
        void UnregisterSubsystem(string subsystemName);

        /// <summary>
        /// Unregisters a subsystem asynchronously.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task UnregisterSubsystemAsync(string subsystemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Captures and returns an immutable snapshot of current system-wide health.
        /// </summary>
        HealthSnapshot GetCurrentSnapshot();

        /// <summary>
        /// Captures and returns an immutable snapshot of current system-wide health asynchronously.
        /// </summary>
        Task<HealthSnapshot> GetCurrentSnapshotAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets historical snapshots captured by the engine.
        /// </summary>
        List<HealthSnapshot> GetHistoricalSnapshots();

        /// <summary>
        /// Gets historical snapshots captured by the engine asynchronously.
        /// </summary>
        Task<List<HealthSnapshot>> GetHistoricalSnapshotsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Captures an immutable snapshot for a single subsystem.
        /// </summary>
        HealthSnapshot GetSubsystemSnapshot(string subsystemName);

        /// <summary>
        /// Captures an immutable snapshot for a single subsystem asynchronously.
        /// </summary>
        Task<HealthSnapshot> GetSubsystemSnapshotAsync(string subsystemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Captures an immutable global snapshot.
        /// </summary>
        HealthSnapshot GetGlobalHealthSnapshot();

        /// <summary>
        /// Captures an immutable global snapshot asynchronously.
        /// </summary>
        Task<HealthSnapshot> GetGlobalHealthSnapshotAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Exposes a text-based summary of current health.
        /// </summary>
        string GetHealthSummary();

        /// <summary>
        /// Exposes a text-based summary of current health asynchronously.
        /// </summary>
        Task<string> GetHealthSummaryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Exposes detailed health info for a subsystem.
        /// </summary>
        SubsystemHealthInfo? GetSubsystemDetails(string subsystemName);

        /// <summary>
        /// Exposes detailed health info for a subsystem asynchronously.
        /// </summary>
        Task<SubsystemHealthInfo?> GetSubsystemDetailsAsync(string subsystemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exposes transition history for a subsystem.
        /// </summary>
        IReadOnlyList<string> GetTransitionHistory(string subsystemName);

        /// <summary>
        /// Exposes transition history for a subsystem asynchronously.
        /// </summary>
        Task<IReadOnlyList<string>> GetTransitionHistoryAsync(string subsystemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exposes a summary of failure statistics.
        /// </summary>
        string GetFailureStatistics();

        /// <summary>
        /// Exposes a summary of failure statistics asynchronously.
        /// </summary>
        Task<string> GetFailureStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Exposes a summary of health scores.
        /// </summary>
        string GetHealthScoreSummary();

        /// <summary>
        /// Exposes a summary of health scores asynchronously.
        /// </summary>
        Task<string> GetHealthScoreSummaryAsync(CancellationToken cancellationToken = default);
    }
}
