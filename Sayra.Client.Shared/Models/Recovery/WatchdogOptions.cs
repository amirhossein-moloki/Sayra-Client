using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Configuration options for the active system Watchdog Service background daemon.
    /// </summary>
    public class WatchdogOptions
    {
        /// <summary>
        /// Gets or sets the background check loop polling frequency.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the duration after which a silent background worker is considered frozen or deadlocked.
        /// </summary>
        public TimeSpan WorkerHeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Gets or sets the warning threshold for queue backlog items.
        /// </summary>
        public int QueueBacklogWarningThreshold { get; set; } = 500;

        /// <summary>
        /// Gets or sets a value indicating whether deadlock and frozen background workers check is active.
        /// </summary>
        public bool EnableDeadlockDetection { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether resource pressure mitigation is managed by the watchdog.
        /// </summary>
        public bool EnableResourcePressureMitigation { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether security violations audits are continuously run.
        /// </summary>
        public bool EnableSecurityViolationAudit { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether offline queue backlog monitoring is active.
        /// </summary>
        public bool EnableQueueBacklogMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether database health and integrity audits are continuously run.
        /// </summary>
        public bool EnableDatabaseHealthChecks { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether workstation network connection active validation is run.
        /// </summary>
        public bool EnableNetworkHealthChecks { get; set; } = true;
    }
}
