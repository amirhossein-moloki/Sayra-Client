using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Enums
{
    /// <summary>
    /// Represents the current execution state of the update lifecycle.
    /// </summary>
    public enum UpdateState
    {
        /// <summary>
        /// The update platform is idle and waiting for instructions.
        /// </summary>
        Idle,

        /// <summary>
        /// The update platform is actively checking for update manifests.
        /// </summary>
        Checking,

        /// <summary>
        /// A newer update package has been detected and is available.
        /// </summary>
        Available,

        /// <summary>
        /// The update package is currently being downloaded.
        /// </summary>
        Downloading,

        /// <summary>
        /// The downloaded package integrity and signatures are being verified.
        /// </summary>
        Verifying,

        /// <summary>
        /// The update files are currently being applied to the workstation.
        /// </summary>
        Installing,

        /// <summary>
        /// The update process completed successfully and the workstation is up-to-date.
        /// </summary>
        Completed,

        /// <summary>
        /// The update process failed.
        /// </summary>
        Failed,

        /// <summary>
        /// An error occurred during installation and rollback procedures have been initiated.
        /// </summary>
        RollingBack,

        /// <summary>
        /// The rollback finished and the workstation returned to the previous stable state.
        /// </summary>
        RolledBack,

        /// <summary>
        /// The update was cancelled by an administrative command.
        /// </summary>
        Cancelled
    }
}
