using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Persists download job progress and chunk states to support resilient resume across restarts and crashes.
    /// </summary>
    public interface IDownloadStateStore
    {
        /// <summary>
        /// Saves the specified download job state securely and transactionally.
        /// </summary>
        Task SaveJobAsync(DownloadJob job, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads the saved download job state for the specified package, or returns null if not found.
        /// </summary>
        Task<DownloadJob?> LoadJobAsync(Guid packageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the saved download job state.
        /// </summary>
        Task DeleteJobAsync(Guid packageId, CancellationToken cancellationToken = default);
    }
}
