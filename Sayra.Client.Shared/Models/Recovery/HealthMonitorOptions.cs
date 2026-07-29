using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Configuration options for the Enterprise Health Monitoring Engine.
    /// </summary>
    public class HealthMonitorOptions
    {
        /// <summary>
        /// Default heartbeat expiration timeout if not specified per subsystem.
        /// </summary>
        public TimeSpan DefaultHeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Heartbeat expiration timeout overrides per subsystem.
        /// </summary>
        public Dictionary<string, TimeSpan> SubsystemHeartbeatTimeouts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Deducted points from health score per recorded failure.
        /// </summary>
        public double BaseFailureDeduction { get; set; } = 10.0;

        /// <summary>
        /// Deducted points from health score per state transition in history.
        /// </summary>
        public double BaseTransitionDeduction { get; set; } = 5.0;

        /// <summary>
        /// Deducted points from health score if a dependency is failed or degraded.
        /// </summary>
        public double DependencyFailureDeduction { get; set; } = 15.0;

        /// <summary>
        /// Maximum number of historical snapshots kept in the memory ring buffer.
        /// </summary>
        public int MaxHistoricalSnapshots { get; set; } = 100;
    }
}
