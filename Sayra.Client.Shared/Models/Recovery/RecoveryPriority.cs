namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the priority level of a subsystem recovery operation.
    /// </summary>
    public enum RecoveryPriority
    {
        /// <summary>
        /// Low priority. Recovery can be deferred or executed during idle periods.
        /// </summary>
        Low,

        /// <summary>
        /// Normal priority. Subsystem recovery is executed under default policy scheduling.
        /// </summary>
        Normal,

        /// <summary>
        /// High priority. Subsystem is important and recovery should take precedence over non-critical work.
        /// </summary>
        High,

        /// <summary>
        /// Critical priority. Urgent recovery. Core system or workstation usability depends on this subsystem.
        /// </summary>
        Critical
    }
}
