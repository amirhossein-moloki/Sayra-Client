using System;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Writes update-specific operational and security events to the Windows Event Log channel.
    /// </summary>
    public interface IWindowsEventLogger
    {
        /// <summary>
        /// Logs that an installation process has started.
        /// </summary>
        /// <param name="targetVersion">The semantic version being installed.</param>
        void LogInstallationStarted(string targetVersion);

        /// <summary>
        /// Logs that an installation has completed successfully.
        /// </summary>
        /// <param name="targetVersion">The semantic version that was installed.</param>
        void LogInstallationCompleted(string targetVersion);

        /// <summary>
        /// Logs that an automated rollback has started.
        /// </summary>
        /// <param name="failedVersion">The version that failed.</param>
        /// <param name="restoredVersion">The safe version being restored.</param>
        void LogRollbackStarted(string failedVersion, string restoredVersion);

        /// <summary>
        /// Logs that a rollback process has completed.
        /// </summary>
        /// <param name="restoredVersion">The restored operational version code.</param>
        void LogRollbackCompleted(string restoredVersion);

        /// <summary>
        /// Logs file verification and integrity anomalies.
        /// </summary>
        /// <param name="filePath">The file that failed verification.</param>
        /// <param name="reason">The failure details.</param>
        void LogVerificationFailure(string filePath, string reason);

        /// <summary>
        /// Logs security and tamper detection alerts.
        /// </summary>
        /// <param name="reason">The description of the security failure.</param>
        void LogSecurityFailure(string reason);
    }
}
