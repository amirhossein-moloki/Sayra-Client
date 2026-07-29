namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the severity level of a subsystem failure or health anomaly.
    /// </summary>
    public enum FailureSeverity
    {
        /// <summary>
        /// Informational or transient diagnostic event. No recovery action needed.
        /// </summary>
        Info,

        /// <summary>
        /// Minor anomaly or warning. Monitored for potential escalation but doesn't immediately degrade core functions.
        /// </summary>
        Warning,

        /// <summary>
        /// Major error. Significant feature or component failed, triggering automated recovery.
        /// </summary>
        Error,

        /// <summary>
        /// Critical failure. An entire subsystem is unusable, impacting dependencies and overall workstation state.
        /// </summary>
        Critical,

        /// <summary>
        /// Fatal/Catastrophic failure. The system cannot recover automatically and requires immediate supervisor or admin notice.
        /// </summary>
        Fatal
    }
}
