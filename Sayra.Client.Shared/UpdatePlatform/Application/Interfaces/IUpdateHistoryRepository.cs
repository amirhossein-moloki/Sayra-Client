using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Handles database persistence and retrieval of update operations history.
    /// </summary>
    public interface IUpdateHistoryRepository
    {
        Task InsertAsync(UpdateHistoryRecord record, CancellationToken cancellationToken = default);
        Task UpdateAsync(UpdateHistoryRecord record, CancellationToken cancellationToken = default);
        Task<UpdateHistoryRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<UpdateHistoryRecord>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<UpdateHistoryRecord?> GetLatestAsync(CancellationToken cancellationToken = default);
        Task CleanupAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);
    }
}
