using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Queues;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Coordinates polling and executing queued transfer jobs.
    /// </summary>
    public interface ITransferScheduler
    {
        /// <summary>
        /// Starts the background scheduling loop.
        /// </summary>
        Task StartAsync(CancellationToken ct = default);

        /// <summary>
        /// Stops the background loop cleanly.
        /// </summary>
        Task StopAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Thread coordinator that polls the TransferQueue and schedules active transfer jobs using the TransferManager.
    /// </summary>
    public class TransferScheduler : ITransferScheduler
    {
        private readonly ITransferQueue _queue;
        private readonly ITransferManager _manager;
        private readonly ILogger<TransferScheduler> _logger;

        private CancellationTokenSource? _loopCts;
        private Task? _schedulerTask;

        /// <summary>
        /// Initializes a new instance of TransferScheduler.
        /// </summary>
        public TransferScheduler(
            ITransferQueue queue,
            ITransferManager manager,
            ILogger<TransferScheduler> logger)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the background scheduling loop.
        /// </summary>
        public Task StartAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Starting TransferScheduler execution loop.");

            _loopCts = new CancellationTokenSource();
            _schedulerTask = Task.Run(() => ProcessQueueLoopAsync(_loopCts.Token), CancellationToken.None);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the background loop cleanly.
        /// </summary>
        public async Task StopAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Stopping TransferScheduler.");

            if (_loopCts != null)
            {
                _loopCts.Cancel();
            }

            if (_schedulerTask != null)
            {
                try
                {
                    await _schedulerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Clean cancellation
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while stopping TransferScheduler.");
                }
            }
        }

        private async Task ProcessQueueLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var job = await _queue.DequeueAsync(ct).ConfigureAwait(false);
                    if (job != null)
                    {
                        _logger.LogInformation("Dequeued job {JobId}. Initiating transfer processing.", job.JobId);
                        await _manager.StartTransferAsync(job, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        // Backoff delay if no jobs are enqueued
                        await Task.Delay(500, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception occurred during TransferScheduler background queue processing.");
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }
            }
        }
    }
}
