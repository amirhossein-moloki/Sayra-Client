using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents structural storage space statistics and constraints on the workstation.
    /// </summary>
    public class StorageStatistics
    {
        public long TotalDiskSpaceBytes { get; set; }
        public long AvailableFreeSpaceBytes { get; set; }
        public long CacheLimitBytes { get; set; }
        public long CurrentCacheSizeBytes { get; set; }
        public long ReservedRollbackSpaceBytes { get; set; }
    }
}
