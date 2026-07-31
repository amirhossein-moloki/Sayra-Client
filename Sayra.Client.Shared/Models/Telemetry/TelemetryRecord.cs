using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.ValueObjects;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents a detailed telemetry metric reading captured from the workstation.
    /// </summary>
    public record TelemetryRecord
    {
        /// <summary>
        /// Gets the exact timestamp of the reading.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the identifier of the workstation machine.
        /// </summary>
        public string MachineId { get; init; } = Environment.MachineName;

        /// <summary>
        /// Gets the unique identifier name of the metric.
        /// </summary>
        public string MetricName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the logical category classification of this metric.
        /// </summary>
        public MetricCategory Category { get; init; }

        /// <summary>
        /// Gets the numeric measurement value of the metric.
        /// </summary>
        public double Value { get; init; }

        /// <summary>
        /// Gets the measurement unit used for the value.
        /// </summary>
        public MetricUnit Unit { get; init; }

        /// <summary>
        /// Gets the source origin of the telemetry record.
        /// </summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>
        /// Gets the current severity rating for the telemetry record.
        /// </summary>
        public MetricSeverity Severity { get; init; } = MetricSeverity.Info;

        /// <summary>
        /// Gets any additional metadata tags associated with the telemetry record.
        /// </summary>
        public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets the logical correlation identifier linking this reading with other operations.
        /// </summary>
        public CorrelationId? CorrelationId { get; init; }
    }
}
