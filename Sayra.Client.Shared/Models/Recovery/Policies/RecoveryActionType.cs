namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Defines the supported self-healing actions that can be orchestrated by the recovery engine.
    /// </summary>
    public enum RecoveryActionType
    {
        /// <summary>
        /// Restarts the host background or supervised worker.
        /// </summary>
        RestartWorker,

        /// <summary>
        /// Reconnects or reinitializes the database connection pool.
        /// </summary>
        ReconnectDatabase,

        /// <summary>
        /// Recycles and re-establishes the TCP connection.
        /// </summary>
        ReconnectTcp,

        /// <summary>
        /// Reloads local and dynamic configuration packages from encrypted storage or server.
        /// </summary>
        ReloadConfiguration,

        /// <summary>
        /// Restarts the secure Named Pipe IPC server.
        /// </summary>
        RestartIpc,

        /// <summary>
        /// Restarts the primary supervised background services.
        /// </summary>
        RestartBackgroundServices,

        /// <summary>
        /// Cleans and restarts interrupted or corrupted downloads.
        /// </summary>
        RestartDownloads,

        /// <summary>
        /// Resets and restarts the persistent database offline queue workers.
        /// </summary>
        RestartQueueWorkers,

        /// <summary>
        /// Re-spawns and sandboxes the external plugin host process.
        /// </summary>
        RestartPluginHost,

        /// <summary>
        /// Re-creates and shows the WPF gameplay security topmost overlay.
        /// </summary>
        RestartOverlay,

        /// <summary>
        /// Escalates the critical failure to remote administration via telemetry/alert channels.
        /// </summary>
        EscalateToAdmin,

        /// <summary>
        /// Forces an orderly graceful reboot of the physical workstation.
        /// </summary>
        RebootWorkstation,

        /// <summary>
        /// Orderly shuts down the physical workstation host.
        /// </summary>
        ShutdownWorkstation
    }
}
