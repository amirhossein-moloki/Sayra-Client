using System;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents a consolidated state snapshot designed to populate real-time administrator dashboards.
    /// </summary>
    public record DashboardSnapshot
    {
        /// <summary>
        /// Gets the exact timestamp when this dashboard snapshot was generated.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

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
        /// Gets the average CPU usage percentage across live systems (0.0 to 100.0).
        /// </summary>
        public double CpuUsagePercent { get; init; }

        /// <summary>
        /// Gets the average Memory usage percentage across live systems (0.0 to 100.0).
        /// </summary>
        public double MemoryUsagePercent { get; init; }

        /// <summary>
        /// Gets the count of system errors or worker thread failure events.
        /// </summary>
        public int FailuresCount { get; init; }

        /// <summary>
        /// Gets the total number of active unhandled alerts.
        /// </summary>
        public int ActiveAlertsCount { get; init; }

        /// <summary>
        /// Gets the current download speed across update pipelines in bytes per second.
        /// </summary>
        public double DownloadsSpeedBytesPerSec { get; init; }

        /// <summary>
        /// Gets the count of workstations with pending system updates.
        /// </summary>
        public int PendingUpdatesCount { get; init; }

        /// <summary>
        /// Gets a value indicating whether network connections are operational and synced.
        /// </summary>
        public bool NetworkConnected { get; init; }

        /// <summary>
        /// Gets the compliance rating of policy configuration applications (0.0 to 100.0).
        /// </summary>
        public double PolicyCompliancePercent { get; init; }

        /// <summary>
        /// Gets a summary statement of active self-healing or crash recovery operations.
        /// </summary>
        public string RecoveryStatusSummary { get; init; } = string.Empty;

        /// <summary>
        /// Gets the count of active security violations or integrity alerts.
        /// </summary>
        public int SecurityViolationsCount { get; init; }
    }
}
