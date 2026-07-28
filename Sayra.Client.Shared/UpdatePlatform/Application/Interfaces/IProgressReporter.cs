using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Computes and aggregates download speed, ETA, overall progress, and chunk-level progress.
    /// </summary>
    public interface IProgressReporter
    {
        /// <summary>
        /// Resets the reporter state for a new download session.
        /// </summary>
        void Reset(Guid jobId, long totalSizeBytes);

        /// <summary>
        /// Registers a progress update of downloaded bytes.
        /// </summary>
        /// <param name="bytesDownloaded">The total bytes downloaded so far.</param>
        void ReportProgress(long bytesDownloaded);

        /// <summary>
        /// Event fired when progress is computed and updated.
        /// </summary>
        event EventHandler<DownloadProgress> ProgressChanged;

        /// <summary>
        /// Gets the current computed download progress snapshot.
        /// </summary>
        DownloadProgress CurrentProgress { get; }
    }
}
