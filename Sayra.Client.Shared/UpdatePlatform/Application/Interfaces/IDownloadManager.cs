using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Represents a service responsible for secure, parallel, and chunked downloads of update packages.
    /// </summary>
    public interface IDownloadManager
    {
        /// <summary>
        /// Downloads the specified update package.
        /// </summary>
        /// <param name="package">The package metadata to download.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DownloadAsync(UpdatePackage package, CancellationToken cancellationToken = default);
    }
}
