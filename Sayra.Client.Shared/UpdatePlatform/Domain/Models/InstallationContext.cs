using System;
using System.IO;
using System.Threading;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the execution context, including target and staging paths, for an active installation pipeline.
    /// </summary>
    public class InstallationContext
    {
        /// <summary>
        /// Gets the active installation job.
        /// </summary>
        public InstallationJob Job { get; }

        /// <summary>
        /// Gets the temporary isolated staging directory path.
        /// </summary>
        public string StagingDirectory { get; }

        /// <summary>
        /// Gets the target production installation directory path.
        /// </summary>
        public string TargetDirectory { get; }

        /// <summary>
        /// Gets the backup directory path of the preceding stable version.
        /// </summary>
        public string BackupDirectory { get; }

        /// <summary>
        /// Gets the cancellation token for the installation execution.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Gets the progress reporter for reporting granular installation progress.
        /// </summary>
        public IProgress<double>? Progress { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationContext"/> class.
        /// </summary>
        public InstallationContext(
            InstallationJob job,
            string stagingDirectory,
            string targetDirectory,
            string backupDirectory,
            CancellationToken cancellationToken,
            IProgress<double>? progress = null)
        {
            Job = job ?? throw new ArgumentNullException(nameof(job));
            StagingDirectory = stagingDirectory ?? throw new ArgumentNullException(nameof(stagingDirectory));
            TargetDirectory = targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory));
            BackupDirectory = backupDirectory ?? throw new ArgumentNullException(nameof(backupDirectory));
            CancellationToken = cancellationToken;
            Progress = progress;
        }
    }
}
