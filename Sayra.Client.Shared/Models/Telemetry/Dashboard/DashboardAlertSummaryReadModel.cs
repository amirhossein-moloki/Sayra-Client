using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents an immutable summary of active, unhandled system alerts.
    /// </summary>
    public record DashboardAlertSummaryReadModel
    {
        /// <summary>
        /// Gets the total count of currently active alerts.
        /// </summary>
        public int ActiveAlertsCount { get; init; }

        /// <summary>
        /// Gets a read-only list of currently active alert records.
        /// </summary>
        public IReadOnlyCollection<AlertRecord> ActiveAlerts { get; init; } = Array.Empty<AlertRecord>();

        /// <summary>
        /// Gets a breakdown of active alerts indexed by their priority level (e.g. Critical, Info, Warning).
        /// </summary>
        public IReadOnlyDictionary<string, int> PriorityBreakdown { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the exact timestamp when this read model was generated.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
