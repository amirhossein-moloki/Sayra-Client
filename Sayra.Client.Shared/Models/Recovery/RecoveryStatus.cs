namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the operational status of an automatic self-healing recovery process.
    /// </summary>
    public enum RecoveryStatus
    {
        /// <summary>
        /// Recovery operation is queued or pending execution.
        /// </summary>
        Pending,

        /// <summary>
        /// Recovery operation is actively in progress.
        /// </summary>
        InProgress,

        /// <summary>
        /// Recovery operation completed successfully, restoring the subsystem to a healthy state.
        /// </summary>
        Success,

        /// <summary>
        /// Recovery operation failed to restore the subsystem.
        /// </summary>
        Failed,

        /// <summary>
        /// Recovery is temporarily suspended during a cooldown/throttling window to prevent retry storms.
        /// </summary>
        Cooldown,

        /// <summary>
        /// Recovery operation was cancelled before completion.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Recovery attempts reached the maximum limit and was aborted to prevent infinite restart loops.
        /// </summary>
        RetriesExceeded
    }
}
