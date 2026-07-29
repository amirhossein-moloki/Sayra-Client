using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
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
        /// <param name="dependencies">A list of subsystem names that this subsystem depends on.</param>
        void RegisterSubsystem(string subsystemName, List<string> dependencies);

        /// <summary>
        /// Registers a subsystem asynchronously.
        /// </summary>
        /// <param name="subsystemName">Name of the subsystem.</param>
        /// <param name="dependencies">A list of subsystem dependencies.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task RegisterSubsystemAsync(string subsystemName, List<string> dependencies, CancellationToken cancellationToken = default);
    }
}
