using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Handles database persistence and retrieval of system rollback operations.
    /// </summary>
    public interface IRollbackHistoryRepository
    {
        Task InsertAsync(RollbackHistoryRecord record, CancellationToken cancellationToken = default);
        Task<RollbackHistoryRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<RollbackHistoryRecord>> GetAllAsync(CancellationToken cancellationToken = default);
        Task CleanupAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);
    }
}
