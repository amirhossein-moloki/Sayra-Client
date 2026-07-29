using System;

namespace Sayra.Client.Shared.Models.Recovery.Events
{
    /// <summary>
    /// Raised when a resource type is detected to be under pressure (crossed a threshold level).
    /// </summary>
    public record ResourcePressureDetectedEvent(
        string CorrelationId,
        string ResourceType,
        double CurrentValue,
        double Threshold,
        string Severity, // e.g. "Warning", "Critical", "Emergency"
        DateTime Timestamp);

    /// <summary>
    /// Raised when a resource type recovers back to normal bounds.
    /// </summary>
    public record ResourcePressureRecoveredEvent(
        string CorrelationId,
        string ResourceType,
        double CurrentValue,
        double Threshold,
        DateTime Timestamp);

    /// <summary>
    /// Raised when any resource threshold is crossed (either going up or down).
    /// </summary>
    public record ResourceThresholdExceededEvent(
        string CorrelationId,
        string ResourceType,
        double CurrentValue,
        double Threshold,
        string Severity, // e.g. "Normal", "Warning", "Critical", "Emergency"
        DateTime Timestamp);

    /// <summary>
    /// Raised every time the system successfully collects and processes a new set of resource metrics.
    /// </summary>
    public record ResourceMetricsCollectedEvent(
        string CorrelationId,
        ResourceMetrics Metrics,
        DateTime Timestamp);
}
