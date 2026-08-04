using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Thread-safe in-memory implementation of ITransferRepository.
    /// </summary>
    public class InMemoryTransferRepository : ITransferRepository
    {
        private readonly ConcurrentDictionary<string, TransferJob> _jobs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Saves or updates a transfer job in memory.
        /// </summary>
        public Task SaveJobAsync(TransferJob job, CancellationToken ct = default)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            if (string.IsNullOrWhiteSpace(job.JobId)) throw new ArgumentException("JobId cannot be empty.", nameof(job));

            _jobs[job.JobId] = job;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves a transfer job by ID.
        /// </summary>
        public Task<TransferJob?> GetJobAsync(string jobId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return Task.FromResult<TransferJob?>(null);

            _jobs.TryGetValue(jobId, out var job);
            return Task.FromResult<TransferJob?>(job);
        }

        /// <summary>
        /// Retrieves all registered transfer jobs.
        /// </summary>
        public Task<IReadOnlyList<TransferJob>> GetAllJobsAsync(CancellationToken ct = default)
        {
            IReadOnlyList<TransferJob> list = _jobs.Values.ToList();
            return Task.FromResult(list);
        }

        /// <summary>
        /// Deletes a transfer job from the in-memory store.
        /// </summary>
        public Task DeleteJobAsync(string jobId, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(jobId))
            {
                _jobs.TryRemove(jobId, out _);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Clears all jobs.
        /// </summary>
        public Task ClearHistoryAsync(CancellationToken ct = default)
        {
            _jobs.Clear();
            return Task.CompletedTask;
        }
    }
}
