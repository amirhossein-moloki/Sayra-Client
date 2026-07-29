using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents the persisted state of the last known application shutdown and startup.
    /// </summary>
    public class PreviousShutdownState
    {
        /// <summary>
        /// Gets or sets the reason for the last shutdown (e.g. Normal, Crash, Forced, Unknown).
        /// </summary>
        public string LastShutdownReason { get; set; } = "Unknown";

        /// <summary>
        /// Gets or sets the timestamp of the last known startup.
        /// </summary>
        public DateTime LastStartupTimestamp { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Gets or sets the timestamp of the last known successful clean shutdown.
        /// </summary>
        public DateTime? LastSuccessfulShutdownTimestamp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether recovery is required on startup.
        /// </summary>
        public bool IsRecoveryRequired { get; set; }
    }
}
