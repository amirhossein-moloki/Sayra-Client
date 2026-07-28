using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Represents the engine responsible for restoring the workstation to a preceding stable version.
    /// </summary>
    public interface IRollbackEngine
    {
        /// <summary>
        /// Executes a full restoration of the workstation's binary state using a saved rollback record.
        /// </summary>
        /// <param name="record">The rollback execution log details.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the rollback restoration completed successfully; otherwise, false.</returns>
        Task<bool> RollbackAsync(RollbackRecord record, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a system backup snapshot before installation.
        /// </summary>
        Task<bool> CreateSnapshotAsync(string snapshotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes rollback to a saved snapshot.
        /// </summary>
        Task<bool> ExecuteRollbackAsync(string snapshotId, string failureReason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that rollback to a saved snapshot succeeded.
        /// </summary>
        Task<bool> ValidateRollbackSucceededAsync(string snapshotId, CancellationToken cancellationToken = default);
    }
}
