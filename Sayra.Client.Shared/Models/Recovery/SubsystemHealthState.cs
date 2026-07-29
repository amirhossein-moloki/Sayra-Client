namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the current health state of a managed subsystem.
    /// </summary>
    public enum SubsystemHealthState
    {
        /// <summary>
        /// The subsystem is healthy, active, and fully operational.
        /// </summary>
        Healthy,

        /// <summary>
        /// The subsystem is experiencing minor anomalies or stale state but remains operational.
        /// </summary>
        Warning,

        /// <summary>
        /// The subsystem has encountered a major failure and is in a critical degraded state.
        /// </summary>
        Critical,

        /// <summary>
        /// The subsystem is completely disabled, stopped, or offline.
        /// </summary>
        Offline
    }
}
