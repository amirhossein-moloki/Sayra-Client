using System;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents the immutable health and status information of an individual system component or subsystem.
    /// </summary>
    public record SubsystemStatus
    {
        /// <summary>
        /// Gets the unique identifier/name of the subsystem.
        /// </summary>
        public string SubsystemName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the health classification (e.g. "Healthy", "Warning", "Critical", "Offline", "Unknown").
        /// </summary>
        public string Health { get; init; } = "Unknown";

        /// <summary>
        /// Gets the current detailed status summary or message from the subsystem.
        /// </summary>
        public string CurrentStatus { get; init; } = "Operational";

        /// <summary>
        /// Gets the timestamp when the subsystem health was last updated or heartbeated.
        /// </summary>
        public DateTime LastUpdated { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets a collection of active warning messages, exception descriptions, or configuration issues currently blocking the subsystem.
        /// </summary>
        public string[] ActiveIssues { get; init; } = Array.Empty<string>();
    }
}
