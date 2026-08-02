using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents an immutable summary of self-healing orchestrations and recorded system failures.
    /// </summary>
    public record DashboardRecoverySummaryReadModel
    {
        /// <summary>
        /// Gets a summary statement of active self-healing or crash recovery operations.
        /// </summary>
        public string RecoveryStatusSummary { get; init; } = string.Empty;

        /// <summary>
        /// Gets the count of system errors or worker thread failure events.
        /// </summary>
        public int FailuresCount { get; init; }

        /// <summary>
        /// Gets the total number of successful recoveries and self-healing events processed.
        /// </summary>
        public int TotalRecoveriesCount { get; init; }

        /// <summary>
        /// Gets a dictionary of failure counts recorded across all subsystems, indexed by subsystem name.
        /// </summary>
        public IReadOnlyDictionary<string, int> SubsystemFailures { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the exact timestamp when this read model was generated.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
