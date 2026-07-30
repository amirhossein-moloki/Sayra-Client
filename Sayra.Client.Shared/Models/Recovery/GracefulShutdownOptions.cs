using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Configuration options specifying individual step and overall timeouts during orderly application shutdown.
    /// </summary>
    public class GracefulShutdownOptions
    {
        /// <summary>
        /// Gets or sets the timeout permitted to stop administrative work and client connections.
        /// </summary>
        public TimeSpan StopWorkTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the timeout permitted to freeze active range downloads.
        /// </summary>
        public TimeSpan StopDownloadsTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the timeout permitted to drain active remote command queues.
        /// </summary>
        public TimeSpan DrainQueuesTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the timeout permitted to flush audit logs to SQLCipher.
        /// </summary>
        public TimeSpan FlushLogsTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the timeout permitted to save current application and session state.
        /// </summary>
        public TimeSpan PersistStatesTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the timeout permitted to stop supervised background workers.
        /// </summary>
        public TimeSpan StopWorkersTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the timeout permitted to close databases cleanly.
        /// </summary>
        public TimeSpan CloseDatabaseTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the absolute timeout ceiling for the entire graceful shutdown workflow.
        /// </summary>
        public TimeSpan OverallTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
