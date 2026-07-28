using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Represents the workstation-specific update manager configuration settings.
    /// </summary>
    public class UpdateOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether automatic update checks and installations are enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the absolute URL of the update orchestration server.
        /// </summary>
        public string UpdateServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the check interval in minutes between background polling sweeps.
        /// </summary>
        public int CheckIntervalMinutes { get; set; } = 180;

        /// <summary>
        /// Gets or sets a value indicating whether update checks are allowed on the Beta channel.
        /// </summary>
        public bool AllowBetaChannel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the client should automatically install downloaded updates during maintenance windows.
        /// </summary>
        public bool AutoInstall { get; set; } = true;

        /// <summary>
        /// Gets or sets the peak time constraint window (e.g., "03:00-05:00") when updates may be silently installed.
        /// </summary>
        public string MaintenanceWindow { get; set; } = string.Empty;
    }
}
