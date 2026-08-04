using System;
using Sayra.Client.Shared.Models.Phase9.Events;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Fleet.Monitoring.Domain.Models;

namespace Sayra.Client.Shared.Fleet.Monitoring.Domain.Events
{
    /// <summary>
    /// Event triggered when real-time live monitoring has been started for a workstation.
    /// </summary>
    public record MonitoringStarted(string MachineId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when real-time live monitoring has been stopped or paused for a workstation.
    /// </summary>
    public record MonitoringStopped(string MachineId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a new immutable telemetry snapshot is successfully generated.
    /// </summary>
    public record SnapshotCreated(string MachineId, Guid SnapshotId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when an existing telemetry snapshot is refreshed or updated.
    /// </summary>
    public record SnapshotUpdated(string MachineId, Guid SnapshotId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a metric value exceeds its configured severity threshold.
    /// </summary>
    public record MetricThresholdExceeded(
        string MachineId,
        string MetricName,
        double CurrentValue,
        double LimitValue,
        MachineHealthStatus Severity) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a previously violated metric recovers back within safe boundaries.
    /// </summary>
    public record MetricRecovered(
        string MachineId,
        string MetricName,
        double CurrentValue,
        double LimitValue) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when contact with a monitored workstation is lost.
    /// </summary>
    public record ConnectionLost(string MachineId, string Reason) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when contact with a monitored workstation is restored.
    /// </summary>
    public record ConnectionRestored(string MachineId) : Phase9BaseEvent;
}
