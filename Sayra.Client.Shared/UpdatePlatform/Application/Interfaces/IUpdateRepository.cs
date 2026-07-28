using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Handles database persistence for the update platform.
    /// </summary>
    public interface IUpdateRepository
    {
        /// <summary>
        /// Saves or updates a history record of an update operation.
        /// </summary>
        /// <param name="entry">The update history entry to save.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveAsync(UpdateHistoryEntry entry, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the history of all update operations on this workstation.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A collection of update history entries.</returns>
        Task<IEnumerable<UpdateHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the latest update history record.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The latest update history entry, or null if none exist.</returns>
        Task<UpdateHistoryEntry?> GetLatestAsync(CancellationToken cancellationToken = default);
    }
}
