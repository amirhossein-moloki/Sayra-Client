using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Governs enterprise bandwidth limiting policies.
    /// </summary>
    public class BandwidthPolicy
    {
        /// <summary>
        /// Gets or sets whether bandwidth throttling is enabled.
        /// </summary>
        public bool ThrottlingEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets maximum speed in bytes per second.
        /// </summary>
        public long MaxBytesPerSecond { get; set; } = 1024 * 1024; // 1 MB/s default

        /// <summary>
        /// Gets or sets whether background download mode is active, which might imply more severe throttling.
        /// </summary>
        public bool IsBackgroundMode { get; set; }
    }
}
