using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Configuration options for the Enterprise Crash Recovery Manager.
    /// </summary>
    public class CrashRecoveryOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether database integrity verification is executed on startup.
        /// </summary>
        public bool EnableDatabaseRepair { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether interrupted range downloads are automatically resumed on startup.
        /// </summary>
        public bool EnableDownloadResume { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether unfinished staged updates are automatically rolled back.
        /// </summary>
        public bool EnableUpdateRollback { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether local cache cleanup is run during crash recovery.
        /// </summary>
        public bool EnableCacheCleanup { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the offline queue database is verified and recreated if corrupted.
        /// </summary>
        public bool EnableQueueVerification { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether pending notifications are recovered.
        /// </summary>
        public bool EnableNotificationRecovery { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether comparison of local and server synchronization is executed.
        /// </summary>
        public bool EnableSyncRecovery { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether policy restoration is done during startup.
        /// </summary>
        public bool EnablePolicyRecovery { get; set; } = true;

        /// <summary>
        /// Gets or sets the file path where the shutdown state is saved/tracked.
        /// </summary>
        public string ShutdownStateFilePath { get; set; } = "Data/shutdown_state.json";
    }
}
