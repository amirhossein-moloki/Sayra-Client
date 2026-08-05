using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Fleet;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;

namespace Sayra.Client.Shared.Fleet.BulkOperations
{
    /// <summary>
    /// Production-grade implementation of IBulkOperationService managing the full lifecycle of bulk operations:
    /// Create, Start, Pause, Resume, Cancel, and Rollback operations.
    /// </summary>
    public class BulkOperationEngine : Sayra.Client.Shared.Interfaces.Phase9.IBulkOperationService
    {
        private readonly IBulkOperationRepository _repository;
        private readonly IBulkOperationCoordinator _coordinator;
        private readonly BulkRollbackManager _rollbackManager;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<BulkOperationEngine> _logger;

        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeCancellations = new();
        private readonly ConcurrentDictionary<string, ManualResetEventSlim> _pauseEvents = new();

        /// <summary>
        /// Initializes a new instance of BulkOperationEngine.
        /// </summary>
        public BulkOperationEngine(
            IBulkOperationRepository repository,
            IBulkOperationCoordinator coordinator,
            BulkRollbackManager rollbackManager,
            IEventDispatcher eventDispatcher,
            ILogger<BulkOperationEngine> _logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _rollbackManager = rollbackManager ?? throw new ArgumentNullException(nameof(rollbackManager));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            this._logger = _logger ?? throw new ArgumentNullException(nameof(_logger));
        }

        /// <summary>
        /// Registers a new bulk operation, targets, and prepares execution state.
        /// </summary>
        public async Task<string> CreateBulkOperationAsync(BulkOperation operation, IEnumerable<BulkOperationTarget> targets, CancellationToken ct = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (targets == null) throw new ArgumentNullException(nameof(targets));

            var opId = string.IsNullOrEmpty(operation.BulkOperationId) ? Guid.NewGuid().ToString() : operation.BulkOperationId;
            var finalOp = operation with { BulkOperationId = opId, Status = OperationStatus.Pending, CreatedAtUtc = DateTime.UtcNow };

            _logger.LogInformation("Creating Bulk Operation '{Id}'. Action={Act}", opId, finalOp.Action);

            await _repository.SaveOperationAsync(finalOp, ct);
            await _repository.SaveTargetsAsync(opId, targets, ct);

            // Publish Created Event
            var listTargets = targets.ToList();
            _eventDispatcher.Dispatch(new BulkOperationCreated(opId, finalOp.Action, listTargets.Count, finalOp.OperatorId));

            return opId;
        }

        /// <inheritdoc />
        public async Task<string> StartBulkOperationAsync(BulkOperation operation, CancellationToken ct = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            var opId = operation.BulkOperationId;
            if (string.IsNullOrEmpty(opId))
            {
                opId = await CreateBulkOperationAsync(operation, Array.Empty<BulkOperationTarget>(), ct);
            }

            var op = await _repository.GetOperationAsync(opId, ct);
            if (op == null)
            {
                throw new InvalidOperationException($"Bulk operation '{opId}' not registered.");
            }

            _logger.LogInformation("Starting Bulk Operation '{Id}'", opId);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _activeCancellations[opId] = cts;

            var pauseEvent = new ManualResetEventSlim(true);
            _pauseEvents[opId] = pauseEvent;

            // Trigger non-blocking asynchronous coordination task in thread pool
            _ = Task.Run(async () =>
            {
                try
                {
                    await _coordinator.RunBulkOperationAsync(op, 10, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Bulk operation '{Id}' was cancelled.", opId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fatal execution failure in Bulk Operation '{Id}'", opId);
                    _eventDispatcher.Dispatch(new BulkOperationFailed(opId, op.Action, ex.Message));
                }
                finally
                {
                    _activeCancellations.TryRemove(opId, out _);
                    _pauseEvents.TryRemove(opId, out _);
                }
            }, ct);

            return opId;
        }

        /// <summary>
        /// Temporarily suspends the dispatching loop of an active bulk operation.
        /// </summary>
        public Task<bool> PauseBulkOperationAsync(string bulkOperationId)
        {
            if (string.IsNullOrEmpty(bulkOperationId)) return Task.FromResult(false);

            if (_pauseEvents.TryGetValue(bulkOperationId, out var pauseEvent))
            {
                _logger.LogInformation("Pausing Bulk Operation '{Id}'", bulkOperationId);
                pauseEvent.Reset(); // Disables subsequent task execution passes
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// Resumes execution of a paused bulk operation.
        /// </summary>
        public Task<bool> ResumeBulkOperationAsync(string bulkOperationId)
        {
            if (string.IsNullOrEmpty(bulkOperationId)) return Task.FromResult(false);

            if (_pauseEvents.TryGetValue(bulkOperationId, out var pauseEvent))
            {
                _logger.LogInformation("Resuming Bulk Operation '{Id}'", bulkOperationId);
                pauseEvent.Set(); // Enables subsequent task execution passes
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <inheritdoc />
        public async Task<bool> CancelBulkOperationAsync(string bulkOperationId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(bulkOperationId)) return false;

            _logger.LogInformation("Requesting Cancellation of Bulk Operation '{Id}'", bulkOperationId);

            var op = await _repository.GetOperationAsync(bulkOperationId, ct);
            if (op == null) return false;

            // Mark in repository as Cancelled
            await _repository.SaveOperationAsync(op with { Status = OperationStatus.Cancelled }, ct);

            if (_activeCancellations.TryRemove(bulkOperationId, out var cts))
            {
                cts.Cancel();
                _eventDispatcher.Dispatch(new CommandCancelled(bulkOperationId, string.Empty, op.Action));
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<BulkOperationProgress?> GetBulkOperationProgressAsync(string bulkOperationId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(bulkOperationId)) return null;
            return await _repository.GetProgressAsync(bulkOperationId, ct);
        }

        /// <summary>
        /// Manually triggers a rollback process to reverse the operation on all successfully reached targets.
        /// </summary>
        public async Task<bool> RollbackBulkOperationAsync(string bulkOperationId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(bulkOperationId)) return false;

            _logger.LogInformation("Manual Rollback requested for Bulk Operation '{Id}'", bulkOperationId);

            var op = await _repository.GetOperationAsync(bulkOperationId, ct);
            if (op == null) return false;

            var executions = await _repository.GetExecutionsAsync(bulkOperationId, ct);
            var succeededMachines = executions
                .Where(e => e.Status == Sayra.Client.Shared.Models.Phase9.Enums.CommandStatus.Succeeded)
                .Select(e => e.MachineId)
                .ToList();

            if (!succeededMachines.Any())
            {
                _logger.LogWarning("No successful target machine executions found to rollback for operation '{Id}'", bulkOperationId);
                return false;
            }

            var rollbackAction = _rollbackManager.GetRollbackAction(op.Action);

            _eventDispatcher.Dispatch(new BulkOperationRollbackStarted(bulkOperationId, rollbackAction, succeededMachines.Count));

            var history = await _rollbackManager.ExecuteRollbackAsync(op, succeededMachines, ct);

            _eventDispatcher.Dispatch(new BulkOperationRollbackCompleted(
                bulkOperationId,
                rollbackAction,
                history.IsValidated,
                history.MachineOutcomes.Values.Count(o => o == OperationResult.Success),
                history.MachineOutcomes.Values.Count(o => o != OperationResult.Success)
            ));

            return history.IsValidated;
        }
    }
}
