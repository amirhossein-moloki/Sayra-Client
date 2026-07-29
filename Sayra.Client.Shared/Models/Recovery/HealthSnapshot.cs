using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents a complete snapshot of the client workstation's subsystem health states and environment.
    /// This model is immutable and serializable.
    /// </summary>
    public class HealthSnapshot
    {
        /// <summary>
        /// Gets the unique identifier for this snapshot.
        /// </summary>
        public Guid SnapshotId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Gets the timestamp when this health snapshot was captured.
        /// </summary>
        public DateTime CapturedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the unique machine name of the workstation.
        /// </summary>
        public string MachineId { get; init; } = Environment.MachineName;

        /// <summary>
        /// Gets the client software version.
        /// </summary>
        public string ClientVersion { get; init; } = "1.0.0";

        /// <summary>
        /// Gets the OS version string.
        /// </summary>
        public string OsVersion { get; init; } = Environment.OSVersion.ToString();

        /// <summary>
        /// Gets the dictionary of subsystem names and their health states at the snapshot moment.
        /// </summary>
        public Dictionary<string, SubsystemHealthState> SubsystemStates { get; init; } = new();

        /// <summary>
        /// Gets detail info objects for each active subsystem at the snapshot moment.
        /// </summary>
        public List<SubsystemHealthInfo> DetailedSubsystems { get; init; } = new();

        /// <summary>
        /// Gets the system resource metrics captured concurrently with this health snapshot, if available.
        /// </summary>
        public ResourceMetrics? Resources { get; init; }
    }
}
