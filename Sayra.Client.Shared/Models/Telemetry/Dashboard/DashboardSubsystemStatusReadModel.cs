using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents an immutable consolidated view of all 15 system-wide components and their individual health.
    /// </summary>
    public record DashboardSubsystemStatusReadModel
    {
        /// <summary>Gets the status summary of the Authentication subsystem.</summary>
        public SubsystemStatus Authentication { get; init; } = new();

        /// <summary>Gets the status summary of the Database subsystem.</summary>
        public SubsystemStatus Database { get; init; } = new();

        /// <summary>Gets the status summary of the Network subsystem.</summary>
        public SubsystemStatus Network { get; init; } = new();

        /// <summary>Gets the status summary of the IPC subsystem.</summary>
        public SubsystemStatus IPC { get; init; } = new();

        /// <summary>Gets the status summary of the Notifications subsystem.</summary>
        public SubsystemStatus Notifications { get; init; } = new();

        /// <summary>Gets the status summary of the Downloads subsystem.</summary>
        public SubsystemStatus Downloads { get; init; } = new();

        /// <summary>Gets the status summary of the Updates subsystem.</summary>
        public SubsystemStatus Updates { get; init; } = new();

        /// <summary>Gets the status summary of the Plugins subsystem.</summary>
        public SubsystemStatus Plugins { get; init; } = new();

        /// <summary>Gets the status summary of the Telemetry subsystem.</summary>
        public SubsystemStatus Telemetry { get; init; } = new();

        /// <summary>Gets the status summary of the Recovery subsystem.</summary>
        public SubsystemStatus Recovery { get; init; } = new();

        /// <summary>Gets the status summary of the Security subsystem.</summary>
        public SubsystemStatus Security { get; init; } = new();

        /// <summary>Gets the status summary of the Policies subsystem.</summary>
        public SubsystemStatus Policies { get; init; } = new();

        /// <summary>Gets the status summary of the Watchdog subsystem.</summary>
        public SubsystemStatus Watchdog { get; init; } = new();

        /// <summary>Gets the status summary of the Overlay subsystem.</summary>
        public SubsystemStatus Overlay { get; init; } = new();

        /// <summary>Gets the status summary of the Synchronization subsystem.</summary>
        public SubsystemStatus Synchronization { get; init; } = new();

        /// <summary>
        /// Gets a dictionary indexing all subsystems by their lower/normalized names for easy dynamic enumeration.
        /// </summary>
        public IReadOnlyDictionary<string, SubsystemStatus> Subsystems { get; init; } = new Dictionary<string, SubsystemStatus>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the exact timestamp when this read model was generated.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
