using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents a persistent download job context for an update package.
    /// </summary>
    public class DownloadJob
    {
        public Guid JobId { get; set; } = Guid.NewGuid();
        public Guid PackageId { get; set; }
        public string Version { get; set; } = string.Empty;
        public long TotalSizeBytes { get; set; }
        public string TargetFilePath { get; set; } = string.Empty;
        public string TempDirectory { get; set; } = string.Empty;
        public List<DownloadChunk> Chunks { get; set; } = new List<DownloadChunk>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Downloading, Paused, Merging, Completed, Failed
    }
}
