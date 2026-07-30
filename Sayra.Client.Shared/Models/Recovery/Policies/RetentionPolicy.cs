using System;

namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Reusable policy governing files, logs, or reports retention limits.
    /// </summary>
    public class RetentionPolicy
    {
        /// <summary>
        /// Gets or sets the maximum count of historical files/items allowed.
        /// </summary>
        public int MaxFileCount { get; set; } = 50;

        /// <summary>
        /// Gets or sets the maximum total size of stored items in bytes.
        /// </summary>
        public long MaxTotalSizeBytes { get; set; } = 50 * 1024 * 1024; // Default 50MB

        /// <summary>
        /// Gets or sets the maximum age of retained historical items.
        /// </summary>
        public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(30);
    }
}
