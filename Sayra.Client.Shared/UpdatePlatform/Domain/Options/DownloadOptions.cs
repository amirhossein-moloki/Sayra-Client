using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Configuration options governing network resource allocation for the package downloader.
    /// </summary>
    public class DownloadOptions
    {
        /// <summary>
        /// Gets or sets the maximum parallel connections/streams to spawn concurrently.
        /// </summary>
        public int MaxParallelDownloads { get; set; } = 2;

        /// <summary>
        /// Gets or sets the absolute bandwidth ceiling limit in Megabits per second.
        /// </summary>
        public double MaxBandwidthMbps { get; set; } = 10.0;

        /// <summary>
        /// Gets or sets the maximum connection failure or timeout retries permitted before aborting.
        /// </summary>
        public int RetryCount { get; set; } = 3;
    }
}
