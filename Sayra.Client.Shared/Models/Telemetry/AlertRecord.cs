using System;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents an active or historical workstation monitoring alert record.
    /// </summary>
    public record AlertRecord
    {
        /// <summary>
        /// Gets the unique identifier for the alert.
        /// </summary>
        public string AlertId { get; init; } = Guid.NewGuid().ToString("D");

        /// <summary>
        /// Gets the exact timestamp when the alert occurred.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the short descriptive name of the alert.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the full message body detailing the alert.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets the category of the metric that triggered the alert.
        /// </summary>
        public MetricCategory Category { get; init; }

        /// <summary>
        /// Gets the severity/priority level of the alert.
        /// </summary>
        public AlertPriority Priority { get; init; } = AlertPriority.Warning;

        /// <summary>
        /// Gets the active workflow status of the alert.
        /// </summary>
        public AlertStatus Status { get; init; } = AlertStatus.Active;

        /// <summary>
        /// Gets the numerical reading that triggered the alert.
        /// </summary>
        public double Value { get; init; }

        /// <summary>
        /// Gets the configured threshold value that was breached.
        /// </summary>
        public double Threshold { get; init; }

        /// <summary>
        /// Gets the system subsystem type associated with this alert.
        /// </summary>
        public SubsystemType Subsystem { get; init; }

        /// <summary>
        /// Gets a value indicating whether the alert has been acknowledged by an administrator.
        /// </summary>
        public bool Acknowledged { get; init; }

        /// <summary>
        /// Gets the identifier of the administrator who acknowledged the alert.
        /// </summary>
        public string? AcknowledgedBy { get; init; }

        /// <summary>
        /// Gets the timestamp when the alert was acknowledged.
        /// </summary>
        public DateTime? AcknowledgedAt { get; init; }

        /// <summary>
        /// Gets a value indicating whether this alert has been escalated.
        /// </summary>
        public bool Escalated { get; init; }

        /// <summary>
        /// Gets the timestamp when this alert was escalated.
        /// </summary>
        public DateTime? EscalatedAt { get; init; }

        /// <summary>
        /// Gets a value indicating whether the alert has been resolved.
        /// </summary>
        public bool Resolved { get; init; }

        /// <summary>
        /// Gets the timestamp when this alert was resolved.
        /// </summary>
        public DateTime? ResolvedAt { get; init; }

        /// <summary>
        /// Gets the comment provided when the alert was acknowledged.
        /// </summary>
        public string? AcknowledgementComment { get; init; }

        /// <summary>
        /// Gets the timestamp when this alert was created.
        /// </summary>
        public DateTime? CreatedAt { get; init; }

        /// <summary>
        /// Gets the timestamp when this alert was suppressed.
        /// </summary>
        public DateTime? SuppressedAt { get; init; }

        /// <summary>
        /// Gets the timestamp when this alert was recovered.
        /// </summary>
        public DateTime? RecoveredAt { get; init; }

        /// <summary>
        /// Gets the timestamp when this alert expired.
        /// </summary>
        public DateTime? ExpiredAt { get; init; }

        /// <summary>
        /// Gets the timestamp when this alert was closed.
        /// </summary>
        public DateTime? ClosedAt { get; init; }
    }
}
