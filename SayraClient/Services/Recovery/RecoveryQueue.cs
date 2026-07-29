using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models.Recovery;

namespace SayraClient.Services.Recovery
{
    public class RecoveryQueueItem
    {
        public string SubsystemName { get; }
        public RecoveryPriority Priority { get; set; }
        public TaskCompletionSource<bool> CompletionSource { get; }
        public CancellationToken CancellationToken { get; }
        public DateTime EnqueuedAt { get; }

        public RecoveryQueueItem(string subsystemName, RecoveryPriority priority, CancellationToken cancellationToken)
        {
            SubsystemName = subsystemName ?? throw new ArgumentNullException(nameof(subsystemName));
            Priority = priority;
            CompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken = cancellationToken;
            EnqueuedAt = DateTime.UtcNow;

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => CompletionSource.TrySetCanceled(cancellationToken));
            }
        }
    }

    public class RecoveryQueue
    {
        private readonly ILogger<RecoveryQueue> _logger;
        private readonly List<RecoveryQueueItem> _queue = new();
        private readonly object _lock = new();
        private readonly SemaphoreSlim _signal = new(0);

        public RecoveryQueue(ILogger<RecoveryQueue> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<bool> EnqueueAsync(string subsystemName, RecoveryPriority priority, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(subsystemName))
            {
                throw new ArgumentException("Subsystem name cannot be empty.", nameof(subsystemName));
            }

            lock (_lock)
            {
                // Deduplication: if already enqueued, reuse or upgrade priority
                var existing = _queue.FirstOrDefault(x => x.SubsystemName.Equals(subsystemName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _logger.LogInformation("Recovery for subsystem '{Subsystem}' already queued. Deduplicating.", subsystemName);
                    if (priority > existing.Priority)
                    {
                        _logger.LogInformation("Upgrading priority of enqueued recovery for '{Subsystem}' from {Old} to {New}.",
                            subsystemName, existing.Priority, priority);
                        existing.Priority = priority;
                    }
                    return existing.CompletionSource.Task;
                }

                var item = new RecoveryQueueItem(subsystemName, priority, cancellationToken);
                _queue.Add(item);
                _logger.LogInformation("Enqueued recovery for subsystem '{Subsystem}' with priority {Priority}.", subsystemName, priority);

                _signal.Release();
                return item.CompletionSource.Task;
            }
        }

        public async Task<RecoveryQueueItem?> DequeueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _signal.WaitAsync(cancellationToken);

                lock (_lock)
                {
                    if (_queue.Count > 0)
                    {
                        // Higher priority first, then oldest enqueued first (FIFO)
                        var item = _queue.OrderByDescending(x => x.Priority).ThenBy(x => x.EnqueuedAt).First();
                        _queue.Remove(item);
                        return item;
                    }
                }
            }

            return null;
        }

        public int GetQueueLength()
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }
}
