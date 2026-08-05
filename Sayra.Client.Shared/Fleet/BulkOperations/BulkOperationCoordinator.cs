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
    /// Coordinator responsible for resolving targets, running the parallel execution pipeline, publishing events,
    /// tracking progress snapshots, and executing rollbacks on failure.
    /// </summary>
    public class BulkOperationCoordinator : IBulkOperationCoordinator
    {
        private readonly IBulkOperationRepository _repository;
        private readonly ITargetResolver _targetResolver;
        private readonly BulkExecutionManager _executionManager;
        private readonly BulkRollbackManager _rollbackManager;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<BulkOperationCoordinator> _logger;

        /// <summary>
        /// Initializes a new instance of BulkOperationCoordinator.
        /// </summary>
        public BulkOperationCoordinator(
            IBulkOperationRepository repository,
            ITargetResolver targetResolver,
            BulkExecutionManager executionManager,
            BulkRollbackManager rollbackManager,
            IEventDispatcher eventDispatcher,
            ILogger<BulkOperationCoordinator> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
            _executionManager = executionManager ?? throw new ArgumentNullException(nameof(executionManager));
            _rollbackManager = rollbackManager ?? throw new ArgumentNullException(nameof(rollbackManager));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<BulkOperationResult> RunBulkOperationAsync(
            BulkOperation operation,
            int maxConcurrency,
            CancellationToken ct = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            _logger.LogInformation("Starting coordination of Bulk Operation '{Id}'", operation.BulkOperationId);

            // 1. Mark operation as Running and publish started event
            var runningOp = operation with { Status = OperationStatus.Running };
            await _repository.SaveOperationAsync(runningOp, ct);

            // 2. Resolve target machines
            var targets = await _repository.GetTargetsAsync(operation.BulkOperationId, ct);
            IReadOnlyList<MachineInfo> resolvedMachines;
            if (targets != null && targets.Any())
            {
                resolvedMachines = await _targetResolver.ResolveTargetsAsync(targets, null, ct);
            }
            else
            {
                // Fallback to direct flat target IDs
                var directTargets = operation.TargetMachineIds.Select(id => new BulkOperationTarget
                {
                    TargetType = BulkTargetType.Individual,
                    TargetValue = id
                });
                resolvedMachines = await _targetResolver.ResolveTargetsAsync(directTargets, null, ct);
            }

            _eventDispatcher.Dispatch(new BulkOperationStarted(operation.BulkOperationId, operation.Action, resolvedMachines.Count));

            if (!resolvedMachines.Any())
            {
                _logger.LogWarning("Bulk operation '{Id}' resolved 0 target machines. Completing as skipped.", operation.BulkOperationId);
                var emptyResult = new BulkOperationResult
                {
                    BulkOperationId = operation.BulkOperationId,
                    Status = OperationStatus.Completed,
                    CombinedDurationMs = 0
                };
                await _repository.SaveResultAsync(emptyResult, ct);
                await _repository.SaveOperationAsync(runningOp with { Status = OperationStatus.Completed }, ct);
                _eventDispatcher.Dispatch(new BulkOperationCompleted(operation.BulkOperationId, operation.Action, 0, 0));
                return emptyResult;
            }

            // 3. Retrieve policy from repository, with fallback to default policy
            var policy = await _repository.GetPolicyAsync(operation.BulkOperationId, ct);
            if (policy == null)
            {
                policy = new BulkOperationPolicy
                {
                    MaxConcurrency = maxConcurrency,
                    IndividualTimeout = TimeSpan.FromSeconds(30),
                    MaxRetries = 2,
                    RetryBaseDelay = TimeSpan.FromSeconds(1),
                    RollbackOnFailure = true // Default automatic rollback on failure
                };
            }

            var tracker = new BulkProgressTracker(operation.BulkOperationId, resolvedMachines.Count);

            // Initialize all machines as pending in tracker
            foreach (var m in resolvedMachines)
            {
                tracker.UpdateMachineState(m.MachineId, Sayra.Client.Shared.Models.Phase9.Enums.CommandStatus.Pending);
                await _repository.SaveExecutionStateAsync(operation.BulkOperationId, new BulkOperationExecution
                {
                    MachineId = m.MachineId,
                    Status = Sayra.Client.Shared.Models.Phase9.Enums.CommandStatus.Pending
                }, ct);
            }

            var startTime = DateTime.UtcNow;

            // 4. Fire Parallel Execution Pipeline
            var pipelineResults = await _executionManager.ExecutePipelineAsync(
                runningOp,
                resolvedMachines,
                policy,
                tracker,
                async (machineId, outcome) =>
                {
                    // Live single task completion callback hook
                    var p = tracker.ComputeProgress();
                    await _repository.SaveProgressAsync(operation.BulkOperationId, p, ct);
                    await _repository.SaveExecutionStateAsync(operation.BulkOperationId, new BulkOperationExecution
                    {
                        MachineId = machineId,
                        Status = outcome.Status,
                        CompletedAtUtc = DateTime.UtcNow
                    }, ct);

                    if (outcome.Outcome != OperationResult.Success)
                    {
                        var failType = (outcome.Outcome == OperationResult.Timeout) ? BulkFailureType.Timeout : BulkFailureType.UnknownFailure;
                        await _repository.SaveFailureAsync(operation.BulkOperationId, new BulkOperationFailure
                        {
                            MachineId = machineId,
                            FailureType = failType,
                            ErrorMessage = outcome.OutputMessage,
                            TimestampUtc = DateTime.UtcNow
                        }, ct);
                    }

                    // Dispatch progress changed event
                    _eventDispatcher.Dispatch(new BulkOperationProgressChanged(
                        operation.BulkOperationId,
                        p.CompletedCount,
                        p.SucceededCount,
                        p.FailedCount,
                        p.PercentageComplete
                    ));
                },
                ct
            );

            var duration = DateTime.UtcNow.Subtract(startTime);

            // 5. Build and persist operation result
            var succeededCount = pipelineResults.Count(r => r.Outcome == OperationResult.Success);
            var failedCount = pipelineResults.Count(r => r.Outcome != OperationResult.Success);

            var finalStatus = (failedCount == 0) ? OperationStatus.Completed :
                              (succeededCount == 0) ? OperationStatus.Failed :
                              OperationStatus.PartiallySucceeded;

            var result = new BulkOperationResult
            {
                BulkOperationId = operation.BulkOperationId,
                Status = finalStatus,
                MachineResults = pipelineResults,
                CombinedDurationMs = (long)duration.TotalMilliseconds
            };

            await _repository.SaveResultAsync(result, ct);
            await _repository.SaveOperationAsync(runningOp with { Status = finalStatus }, ct);

            _eventDispatcher.Dispatch(new BulkOperationCompleted(operation.BulkOperationId, operation.Action, succeededCount, failedCount));

            // 6. Handle automatic Rollback if configured on failure
            if (policy.RollbackOnFailure && failedCount > 0)
            {
                _logger.LogWarning("Bulk operation '{Id}' has failed tasks and policy RollbackOnFailure is active. Starting rollback.", operation.BulkOperationId);
                var rollbackAction = _rollbackManager.GetRollbackAction(operation.Action);

                _eventDispatcher.Dispatch(new BulkOperationRollbackStarted(operation.BulkOperationId, rollbackAction, resolvedMachines.Count));

                var rollbackHistory = await _rollbackManager.ExecuteRollbackAsync(operation, resolvedMachines.Select(m => m.MachineId), ct);

                _eventDispatcher.Dispatch(new BulkOperationRollbackCompleted(
                    operation.BulkOperationId,
                    rollbackAction,
                    rollbackHistory.IsValidated,
                    rollbackHistory.MachineOutcomes.Values.Count(o => o == OperationResult.Success),
                    rollbackHistory.MachineOutcomes.Values.Count(o => o != OperationResult.Success)
                ));
            }

            return result;
        }
    }
}
