using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Repository interface for persisting and querying transfer jobs, history, and statistics.
    /// </summary>
    public interface ITransferRepository
    {
        /// <summary>
        /// Saves or updates a transfer job.
        /// </summary>
        Task SaveJobAsync(TransferJob job, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a transfer job by ID.
        /// </summary>
        Task<TransferJob?> GetJobAsync(string jobId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all registered transfer jobs.
        /// </summary>
        Task<IReadOnlyList<TransferJob>> GetAllJobsAsync(CancellationToken ct = default);

        /// <summary>
        /// Deletes a transfer job from persistence.
        /// </summary>
        Task DeleteJobAsync(string jobId, CancellationToken ct = default);

        /// <summary>
        /// Clears all completed or failed transfer jobs from the active set.
        /// </summary>
        Task ClearHistoryAsync(CancellationToken ct = default);
    }
}
