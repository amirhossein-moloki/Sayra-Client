using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Configuration options governing workstation automatic backup and system state rollbacks.
    /// </summary>
    public class RollbackOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether workstation rollback and snapshots are enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum history count of system version backup snapshots to retain on disk.
        /// </summary>
        public int MaxRollbackVersions { get; set; } = 3;

        /// <summary>
        /// Gets or sets the expiration lifetime in days of historical system backup snapshots.
        /// </summary>
        public int SnapshotRetentionDays { get; set; } = 30;
    }
}
