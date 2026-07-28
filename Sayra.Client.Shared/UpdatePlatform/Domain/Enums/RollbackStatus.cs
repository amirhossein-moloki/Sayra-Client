using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Enums
{
    /// <summary>
    /// Represents the state of the system restoration or rollback execution.
    /// </summary>
    public enum RollbackStatus
    {
        /// <summary>
        /// Rollback procedures are not required for this lifecycle session.
        /// </summary>
        NotRequired,

        /// <summary>
        /// Previous system snapshot is validated and available for recovery.
        /// </summary>
        Available,

        /// <summary>
        /// System restoration has been initiated.
        /// </summary>
        Started,

        /// <summary>
        /// Rollback completed successfully and previous stable state has been restored.
        /// </summary>
        Completed,

        /// <summary>
        /// Rollback failed, leaving system in a quarantined state.
        /// </summary>
        Failed
    }
}
