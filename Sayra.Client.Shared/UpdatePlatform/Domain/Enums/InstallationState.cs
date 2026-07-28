using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Enums
{
    /// <summary>
    /// Represents the deterministic states of the update installation lifecycle.
    /// </summary>
    public enum InstallationState
    {
        /// <summary>
        /// The installation process is idle and has not started.
        /// </summary>
        Idle,

        /// <summary>
        /// Initial preparation of staging directories and locking checks.
        /// </summary>
        Preparing,

        /// <summary>
        /// Validating update package metadata, prerequisites, and digital trust.
        /// </summary>
        Validating,

        /// <summary>
        /// Unpacking and staging the update files in the isolated temp workspace.
        /// </summary>
        Staging,

        /// <summary>
        /// Gracefully shutting down the active services, WPF UI shells, and processes.
        /// </summary>
        StoppingServices,

        /// <summary>
        /// Performing the safe atomic byte and file-level replacement of target binaries.
        /// </summary>
        Installing,

        /// <summary>
        /// Post-installation integrity validations including hash and version checks.
        /// </summary>
        Verifying,

        /// <summary>
        /// Restarting applications, services, and visual shell environments.
        /// </summary>
        Restarting,

        /// <summary>
        /// The update and verification completed with absolute success.
        /// </summary>
        Completed,

        /// <summary>
        /// The installation process failed during execution.
        /// </summary>
        Failed
    }
}
