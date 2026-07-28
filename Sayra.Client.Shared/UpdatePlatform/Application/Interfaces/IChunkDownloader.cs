using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Responsible for downloading individual chunks of a package from specified mirrors, supporting resume, retries, and throttling.
    /// </summary>
    public interface IChunkDownloader
    {
        /// <summary>
        /// Downloads a single chunk range from a mirror and writes it to the local temporary file.
        /// </summary>
        /// <param name="chunk">The chunk configuration details.</param>
        /// <param name="package">The parent update package.</param>
        /// <param name="mirror">The mirror endpoint to fetch from.</param>
        /// <param name="progressReporter">The progress reporter to notify of downloaded bytes.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the download operation.</returns>
        Task DownloadChunkAsync(
            DownloadChunk chunk,
            UpdatePackage package,
            MirrorEndpoint mirror,
            IProgressReporter progressReporter,
            CancellationToken cancellationToken = default);
    }
}
