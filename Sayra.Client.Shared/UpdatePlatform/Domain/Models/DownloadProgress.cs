using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents overall progress of an active download job.
    /// </summary>
    public class DownloadProgress
    {
        public Guid JobId { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalSizeBytes { get; set; }
        public double Percentage => TotalSizeBytes > 0 ? (double)BytesDownloaded / TotalSizeBytes * 100.0 : 0.0;
        public double DownloadSpeedBytesPerSecond { get; set; } // Bytes/sec
        public TimeSpan EstimatedTimeRemaining { get; set; } = TimeSpan.Zero;
    }
}
