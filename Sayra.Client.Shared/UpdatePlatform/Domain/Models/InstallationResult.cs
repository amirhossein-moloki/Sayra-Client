using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the immutable final result of an installation process execution.
    /// </summary>
    public class InstallationResult
    {
        /// <summary>
        /// Gets a value indicating whether the installation was successful.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the error message if the installation failed.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets a value indicating whether a system restart or application restart is required.
        /// </summary>
        public bool RestartRequired { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationResult"/> class.
        /// </summary>
        /// <param name="success">Whether the installation succeeded.</param>
        /// <param name="errorMessage">The error message if failed.</param>
        /// <param name="restartRequired">Whether restart is required.</param>
        public InstallationResult(bool success, string? errorMessage = null, bool restartRequired = false)
        {
            Success = success;
            ErrorMessage = errorMessage;
            RestartRequired = restartRequired;
        }

        /// <summary>
        /// Creates a successful installation result.
        /// </summary>
        public static InstallationResult Successful(bool restartRequired = false) => new InstallationResult(true, null, restartRequired);

        /// <summary>
        /// Creates a failed installation result.
        /// </summary>
        public static InstallationResult Failed(string errorMessage, bool restartRequired = false) => new InstallationResult(false, errorMessage, restartRequired);
    }
}
