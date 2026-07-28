using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Central coordinator responsible for secure, parallel, resumable chunked updates package downloads,
    /// complete with bandwidth limitations, mirror fallback, and detailed progress reporting.
    /// </summary>
    public interface IDownloadManager
    {
        /// <summary>
        /// Event fired when overall progress is updated.
        /// </summary>
        event EventHandler<DownloadProgress> ProgressChanged;

        /// <summary>
        /// Downloads the specified update package, managing parallel tasks, resumption, and merge operations.
        /// </summary>
        /// <param name="package">The package metadata to download.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task containing the path to the merged, verified package file.</returns>
        Task<string> DownloadAsync(UpdatePackage package, CancellationToken cancellationToken = default);

        /// <summary>
        /// Configures the current bandwidth policy.
        /// </summary>
        /// <param name="policy">The policy details.</param>
        void ConfigureBandwidthPolicy(BandwidthPolicy policy);
    }
}
