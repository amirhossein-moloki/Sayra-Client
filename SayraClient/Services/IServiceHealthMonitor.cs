using System;
using System.Collections.Generic;

namespace SayraClient.Services;

/// <summary>
/// Detailed health states for supervised background workers and hosted services.
/// </summary>
public enum ServiceHealthState
{
    Starting,
    Healthy,
    Degraded,
    Recovering,
    Failed,
    Stopped
}

/// <summary>
/// Snapshot of health state for a single tracked service.
/// </summary>
public class ServiceHealthInfo
{
    public string ServiceName { get; init; } = string.Empty;
    public ServiceHealthState State { get; set; } = ServiceHealthState.Starting;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public int FailureCount { get; set; }
    public string? LastMessage { get; set; }
    public Exception? LastException { get; set; }
}

/// <summary>
/// Monitors and tracks the health of all background workers and hosted services.
/// </summary>
public interface IServiceHealthMonitor
{
    /// <summary>
    /// Event raised when a tracked service transitions to a new health state.
    /// </summary>
    event Action<string, ServiceHealthState, ServiceHealthState>? HealthStateChanged;

    /// <summary>
    /// Reports a heartbeat/alive signal from a tracked service.
    /// </summary>
    void ReportHeartbeat(string serviceName);

    /// <summary>
    /// Reports the health state of a tracked service.
    /// </summary>
    void ReportState(string serviceName, ServiceHealthState state, string? message = null);

    /// <summary>
    /// Reports a service failure with an associated exception.
    /// </summary>
    void ReportFailure(string serviceName, Exception exception, string? message = null);

    /// <summary>
    /// Calculates and returns the overall system health state based on all registered services.
    /// </summary>
    ServiceHealthState GetOverallHealth();

    /// <summary>
    /// Returns a snapshot of the health info for all registered services.
    /// </summary>
    IReadOnlyDictionary<string, ServiceHealthInfo> GetDetailedHealth();
}
