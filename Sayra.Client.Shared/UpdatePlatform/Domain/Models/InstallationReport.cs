using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the summary report of an installation run.
    /// </summary>
    public class InstallationReport
    {
        /// <summary>
        /// Gets or sets the unique job ID.
        /// </summary>
        public Guid JobId { get; set; }

        /// <summary>
        /// Gets or sets the version installed.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the installation started.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets when the installation completed (or failed).
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets whether the installation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the descriptive details of the installation (files installed, reasons for failure, etc.).
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Gets the total duration of the installation process.
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;
    }
}
