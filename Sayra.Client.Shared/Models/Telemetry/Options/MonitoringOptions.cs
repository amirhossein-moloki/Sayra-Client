using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing workstation heartbeat and active process integrity checks.
    /// </summary>
    public class MonitoringOptions
    {
        /// <summary>
        /// Gets or sets the threshold timeout duration for detecting missing heartbeats in seconds.
        /// </summary>
        [Range(1, 120, ErrorMessage = "HeartbeatTimeoutSeconds must be between 1 and 120.")]
        public int HeartbeatTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether background kernel monitoring checks for process tampering are active.
        /// </summary>
        public bool EnableProcessTamperingCheck { get; set; } = true;
    }
}
