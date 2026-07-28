using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Represents configuration options for update storage, databases, and cache quotas.
    /// </summary>
    public class StorageOptions
    {
        /// <summary>
        /// Gets or sets the custom path to the local update cache. If empty, a default path is resolved.
        /// </summary>
        public string CacheDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the custom path to the local update database. If empty, a default path is resolved.
        /// </summary>
        public string DatabasePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum size allowed for cache files in Megabytes.
        /// </summary>
        public long MaxCacheSizeMegabytes { get; set; } = 1024; // 1 GB default

        /// <summary>
        /// Gets or sets the reserved disk space in Megabytes for system rollback snapshots.
        /// </summary>
        public long ReservedRollbackSpaceMegabytes { get; set; } = 250; // 250 MB default

        /// <summary>
        /// Gets or sets the number of days after which a cache entry is considered expired.
        /// </summary>
        public int CacheExpirationDays { get; set; } = 14; // 14 days default
    }
}
