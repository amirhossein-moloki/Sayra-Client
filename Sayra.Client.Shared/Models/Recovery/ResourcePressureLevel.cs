namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the level of resource pressure (CPU, RAM, Disk, Handles) on the workstation.
    /// </summary>
    public enum ResourcePressureLevel
    {
        /// <summary>
        /// Resource usage is normal and well within acceptable thresholds.
        /// </summary>
        Normal,

        /// <summary>
        /// Low resource pressure. System performance is healthy but minor warning markers might have been crossed.
        /// </summary>
        Low,

        /// <summary>
        /// Moderate resource pressure. The system may start throttling non-critical work or optimizing local caches.
        /// </summary>
        Medium,

        /// <summary>
        /// High resource pressure. Systems must actively apply backpressure, defer sync tasks, and free cached storage.
        /// </summary>
        High,

        /// <summary>
        /// Critical resource pressure. Workstation usability is at risk. Immediate aggressive mitigation required.
        /// </summary>
        Critical
    }
}
