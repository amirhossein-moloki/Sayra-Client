using System;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents an immutable high-level overview of the entire center workstation health and user activity.
    /// </summary>
    public record DashboardOverviewReadModel
    {
        /// <summary>
        /// Gets the number of currently active/live machines in the center.
        /// </summary>
        public int LiveMachinesCount { get; init; }

        /// <summary>
        /// Gets the number of currently authenticated online users.
        /// </summary>
        public int OnlineUsersCount { get; init; }

        /// <summary>
        /// Gets the number of actively running game processes.
        /// </summary>
        public int RunningGamesCount { get; init; }

        /// <summary>
        /// Gets the overall health rating or status of the system (e.g., "Healthy", "Warning", "Critical").
        /// </summary>
        public string OverallHealthStatus { get; init; } = "Healthy";

        /// <summary>
        /// Gets the exact timestamp when this read model was generated.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
