using System;
using System.Collections.Generic;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the parameters and state of a specific installation task execution.
    /// </summary>
    public class InstallationJob
    {
        /// <summary>
        /// Gets or sets the unique identifier of the installation job.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the metadata of the update package to be installed.
        /// </summary>
        public UpdatePackage Package { get; set; } = new UpdatePackage();

        /// <summary>
        /// Gets or sets the path to the physical update package on the local machine.
        /// </summary>
        public string PackagePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current installation progress percentage (0.0 to 100.0).
        /// </summary>
        public double ProgressPercentage { get; set; }

        /// <summary>
        /// Gets or sets the current state of this specific job in the state machine.
        /// </summary>
        public InstallationState State { get; set; } = InstallationState.Idle;

        /// <summary>
        /// Gets or sets the error message if the installation fails.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this job was registered.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the timestamp when the job execution was completed or failed.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the dictionary of staged files and their pre-installation SHA-256 hashes.
        /// </summary>
        public Dictionary<string, string> StagedFiles { get; set; } = new Dictionary<string, string>();
    }
}
