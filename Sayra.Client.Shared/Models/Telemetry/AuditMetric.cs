using System;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents an individual enterprise audit activity tracking metric.
    /// </summary>
    public record AuditMetric
    {
        /// <summary>
        /// Gets the unique identifier for the audit event.
        /// </summary>
        public string AuditId { get; init; } = Guid.NewGuid().ToString("D");

        /// <summary>
        /// Gets the timestamp when the audit event occurred.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the identifying name of the metric (e.g. LoginCount, SessionDuration, GameLaunches).
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the workstation machine identifier.
        /// </summary>
        public string MachineId { get; init; } = Environment.MachineName;

        /// <summary>
        /// Gets the user session identifier, if applicable.
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// Gets the logged-on user identifier, if applicable.
        /// </summary>
        public string? UserId { get; init; }

        /// <summary>
        /// Gets the operator or administrator identifier who executed the audited action.
        /// </summary>
        public string? OperatorId { get; init; }

        /// <summary>
        /// Gets detailed descriptive parameters or payloads of the action.
        /// </summary>
        public string Details { get; init; } = string.Empty;

        /// <summary>
        /// Gets any occurrences count associated with this operational event.
        /// </summary>
        public long Count { get; init; }

        /// <summary>
        /// Gets the duration of the audited operation, if applicable.
        /// </summary>
        public TimeSpan Duration { get; init; } = TimeSpan.Zero;
    }
}
