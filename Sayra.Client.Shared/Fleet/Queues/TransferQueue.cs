using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Queues
{
    /// <summary>
    /// Coordinates transfer job scheduling and prioritisation.
    /// </summary>
    public interface ITransferQueue
    {
        /// <summary>
        /// Enqueues a job based on its priority or category.
        /// </summary>
        Task<bool> EnqueueAsync(TransferJob job, CancellationToken ct = default);

        /// <summary>
        /// Dequeues the next highest priority pending job.
        /// </summary>
        Task<TransferJob?> DequeueAsync(CancellationToken ct = default);

        /// <summary>
        /// Moves a job to the retry queue.
        /// </summary>
        Task MoveToRetryAsync(string jobId, CancellationToken ct = default);

        /// <summary>
        /// Moves a job to the failed queue.
        /// </summary>
        Task MoveToFailedAsync(string jobId, string errorMessage, CancellationToken ct = default);

        /// <summary>
        /// Recovers all pending, failed, or paused transfer jobs upon system startup/restart.
        /// </summary>
        Task RecoverJobsAfterRestartAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Thread-safe transfer queue manager supporting priorities, retry/failed statuses, and startup recovery.
    /// </summary>
    public class TransferQueue : ITransferQueue
    {
        private readonly ITransferRepository _repository;
        private readonly ConcurrentQueue<string> _highPriorityQueue = new();
        private readonly ConcurrentQueue<string> _normalPriorityQueue = new();
        private readonly ConcurrentQueue<string> _backgroundPriorityQueue = new();
        private readonly ConcurrentDictionary<string, byte> _enqueuedPaths = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of TransferQueue.
        /// </summary>
        public TransferQueue(ITransferRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// Enqueues a job, performing duplicate detection on both JobId and FilePath.
        /// </summary>
        public async Task<bool> EnqueueAsync(TransferJob job, CancellationToken ct = default)
        {
            if (job == null) return false;

            // Duplicate detection on FilePath and JobId
            if (_enqueuedPaths.ContainsKey(job.FilePath))
            {
                return false;
            }

            var existing = await _repository.GetJobAsync(job.JobId, ct).ConfigureAwait(false);
            if (existing != null && (existing.Status == TransferStatus.Transferring || existing.Status == TransferStatus.Completed))
            {
                return false;
            }

            _enqueuedPaths.TryAdd(job.FilePath, 0);

            // Save job state to repository for persistence/recovery
            var queuedJob = job with { Status = TransferStatus.Pending };
            await _repository.SaveJobAsync(queuedJob, ct).ConfigureAwait(false);

            // Segment queue by priority: UpdatePackage and DiagnosticBundle get prioritized over normal File
            if (job.Category == TransferType.UpdatePackage || job.Category == TransferType.Configuration)
            {
                _highPriorityQueue.Enqueue(job.JobId);
            }
            else if (job.Category == TransferType.MediaAsset)
            {
                _backgroundPriorityQueue.Enqueue(job.JobId);
            }
            else
            {
                _normalPriorityQueue.Enqueue(job.JobId);
            }

            return true;
        }

        /// <summary>
        /// Dequeues the next highest priority pending job.
        /// </summary>
        public async Task<TransferJob?> DequeueAsync(CancellationToken ct = default)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                string? jobId = null;

                // Priority dequeuing order: High -> Normal -> Background
                if (_highPriorityQueue.TryDequeue(out var highId))
                {
                    jobId = highId;
                }
                else if (_normalPriorityQueue.TryDequeue(out var normalId))
                {
                    jobId = normalId;
                }
                else if (_backgroundPriorityQueue.TryDequeue(out var bgId))
                {
                    jobId = bgId;
                }

                if (jobId == null) return null;

                var job = await _repository.GetJobAsync(jobId, ct).ConfigureAwait(false);
                if (job != null && job.Status == TransferStatus.Pending)
                {
                    _enqueuedPaths.TryRemove(job.FilePath, out _);
                    return job;
                }

                // If job is cancelled/failed/completed, remove it from enqueued paths and find next
                if (job != null)
                {
                    _enqueuedPaths.TryRemove(job.FilePath, out _);
                }
            }
        }

        /// <summary>
        /// Moves a job to retry state and places it back in the queue.
        /// </summary>
        public async Task MoveToRetryAsync(string jobId, CancellationToken ct = default)
        {
            var job = await _repository.GetJobAsync(jobId, ct).ConfigureAwait(false);
            if (job == null) return;

            var updatedJob = job with { Status = TransferStatus.Pending };
            await _repository.SaveJobAsync(updatedJob, ct).ConfigureAwait(false);

            // Re-enqueue in the correct priority queue
            if (job.Category == TransferType.UpdatePackage || job.Category == TransferType.Configuration)
            {
                _highPriorityQueue.Enqueue(jobId);
            }
            else if (job.Category == TransferType.MediaAsset)
            {
                _backgroundPriorityQueue.Enqueue(jobId);
            }
            else
            {
                _normalPriorityQueue.Enqueue(jobId);
            }
        }

        /// <summary>
        /// Marks a job as failed in persistence and clears it from active paths.
        /// </summary>
        public async Task MoveToFailedAsync(string jobId, string errorMessage, CancellationToken ct = default)
        {
            var job = await _repository.GetJobAsync(jobId, ct).ConfigureAwait(false);
            if (job == null) return;

            var updatedJob = job with { Status = TransferStatus.Failed };
            await _repository.SaveJobAsync(updatedJob, ct).ConfigureAwait(false);
            _enqueuedPaths.TryRemove(job.FilePath, out _);
        }

        /// <summary>
        /// Scans persistence on startup and enqueues interrupted/pending jobs.
        /// </summary>
        public async Task RecoverJobsAfterRestartAsync(CancellationToken ct = default)
        {
            var allJobs = await _repository.GetAllJobsAsync(ct).ConfigureAwait(false);
            foreach (var job in allJobs)
            {
                // Recover jobs that were left in Pending, Preparing, Transferring, or Paused states
                if (job.Status == TransferStatus.Pending ||
                    job.Status == TransferStatus.Preparing ||
                    job.Status == TransferStatus.Transferring ||
                    job.Status == TransferStatus.Paused)
                {
                    // Reset status to Pending to allow clean retry/resume
                    var recoveredJob = job with { Status = TransferStatus.Pending };
                    await _repository.SaveJobAsync(recoveredJob, ct).ConfigureAwait(false);

                    _enqueuedPaths.TryAdd(job.FilePath, 0);

                    if (job.Category == TransferType.UpdatePackage || job.Category == TransferType.Configuration)
                    {
                        _highPriorityQueue.Enqueue(job.JobId);
                    }
                    else if (job.Category == TransferType.MediaAsset)
                    {
                        _backgroundPriorityQueue.Enqueue(job.JobId);
                    }
                    else
                    {
                        _normalPriorityQueue.Enqueue(job.JobId);
                    }
                }
            }
        }
    }
}
