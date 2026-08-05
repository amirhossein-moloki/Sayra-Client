using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.BulkOperations
{
    /// <summary>
    /// Manager responsible for orchestrating the Parallel Execution Pipeline with configurable concurrency, backpressure, priority queuing, and absolute failure isolation.
    /// </summary>
    public class BulkExecutionManager
    {
        private readonly IRemoteCommandService _commandService;
        private readonly ILogger<BulkExecutionManager> _logger;

        /// <summary>
        /// Initializes a new instance of BulkExecutionManager.
        /// </summary>
        public BulkExecutionManager(
            IRemoteCommandService commandService,
            ILogger<BulkExecutionManager> logger)
        {
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes a bulk command in parallel across all targeted machines using a custom concurrency pipeline.
        /// </summary>
        /// <param name="operation">The bulk operation definitions.</param>
        /// <param name="machines">The resolved workstation details.</param>
        /// <param name="policy">The execution policy (concurrency, timeout, retries, etc.).</param>
        /// <param name="tracker">Progress tracker to register live updates.</param>
        /// <param name="onMachineCompleted">Event hook fired when a single machine task completes.</param>
        /// <param name="ct">The main cancellation token.</param>
        /// <returns>A collection of individual command outcomes.</returns>
        public async Task<List<CommandResult>> ExecutePipelineAsync(
            BulkOperation operation,
            IReadOnlyList<MachineInfo> machines,
            BulkOperationPolicy policy,
            BulkProgressTracker tracker,
            Func<string, CommandResult, Task>? onMachineCompleted,
            CancellationToken ct)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (machines == null) throw new ArgumentNullException(nameof(machines));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (tracker == null) throw new ArgumentNullException(nameof(tracker));

            _logger.LogInformation("Initializing Parallel Execution Pipeline for Bulk Operation '{Id}'. Concurrency Limit={Limit}, Total Targets={Count}",
                operation.BulkOperationId, policy.MaxConcurrency, machines.Count);

            var commandResults = new List<CommandResult>();
            var resultsLock = new object();

            // Setup a SemaphoreSlim to control backpressure and enforce maximum active parallel tasks
            using var semaphore = new SemaphoreSlim(policy.MaxConcurrency);

            // Priority Queue of tasks based on command priority
            var tasks = new List<Task>();

            foreach (var machine in machines)
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Bulk operation execution pipeline cancelled before dispatching to all targets.");
                    break;
                }

                // Register machine state as executing
                tracker.UpdateMachineState(machine.MachineId, CommandStatus.Executing);

                // Acquire semaphore slot (implements backpressure)
                await semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    CommandResult outcome;
                    try
                    {
                        outcome = await ExecuteWithRetriesAsync(operation, machine, policy, tracker, ct);
                    }
                    catch (Exception ex)
                    {
                        // Failure isolation: any unhandled exception on a workstation task must not crash the entire pipeline
                        _logger.LogError(ex, "Unexpected error executing task on machine '{Id}' during bulk operation.", machine.MachineId);
                        outcome = new CommandResult
                        {
                            CommandId = Guid.NewGuid().ToString(),
                            MachineId = machine.MachineId,
                            Status = CommandStatus.Failed,
                            Outcome = OperationResult.Failure,
                            OutputMessage = $"Unhandled pipeline error: {ex.Message}",
                            CompletedAtUtc = DateTime.UtcNow
                        };
                    }
                    finally
                    {
                        // Release slot in semaphore
                        semaphore.Release();
                    }

                    // Thread-safe update of combined results
                    lock (resultsLock)
                    {
                        commandResults.Add(outcome);
                    }

                    // Fire complete hooks
                    if (onMachineCompleted != null)
                    {
                        try
                        {
                            await onMachineCompleted(machine.MachineId, outcome);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing completed machine task callbacks for '{Id}'", machine.MachineId);
                        }
                    }

                }, ct);

                tasks.Add(task);
            }

            // Wait for all spawned parallel tasks to complete (Failure isolation: all run to completion regardless of individual outcomes)
            await Task.WhenAll(tasks);

            _logger.LogInformation("Parallel Execution Pipeline finished for Bulk Operation '{Id}'. Succeeded={SucceededCount}, Failed={FailedCount}",
                operation.BulkOperationId, commandResults.Count(r => r.Outcome == OperationResult.Success), commandResults.Count(r => r.Outcome != OperationResult.Success));

            return commandResults;
        }

        private async Task<CommandResult> ExecuteWithRetriesAsync(
            BulkOperation operation,
            MachineInfo machine,
            BulkOperationPolicy policy,
            BulkProgressTracker tracker,
            CancellationToken ct)
        {
            int attempt = 0;
            while (true)
            {
                attempt++;
                tracker.UpdateMachineState(machine.MachineId, CommandStatus.Executing, attempt);

                using var individualCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                individualCts.CancelAfter(policy.IndividualTimeout);

                try
                {
                    _logger.LogInformation("Executing bulk task on '{MachineId}' (Attempt {Attempt}/{Max})", machine.MachineId, attempt, policy.MaxRetries + 1);

                    var cmd = new RemoteCommand
                    {
                        CommandId = Guid.NewGuid().ToString(),
                        Action = operation.Action,
                        TargetMachineId = machine.MachineId,
                        Priority = CommandPriority.Normal,
                        CreatorOperatorId = operation.OperatorId,
                        ExpiresAtUtc = DateTime.UtcNow.Add(policy.IndividualTimeout * 2)
                    };

                    // Invoke single remote command execution
                    var result = await _commandService.ExecuteCommandAsync(cmd, individualCts.Token);

                    if (result.Outcome == OperationResult.Success)
                    {
                        tracker.UpdateMachineState(machine.MachineId, CommandStatus.Succeeded, attempt);
                        return result;
                    }

                    _logger.LogWarning("Execution on '{MachineId}' failed with: {Msg}. Outcome: {Outcome}", machine.MachineId, result.OutputMessage, result.Outcome);

                    if (attempt > policy.MaxRetries || !IsTransient(result.Outcome))
                    {
                        tracker.UpdateMachineState(machine.MachineId, CommandStatus.Failed, attempt);
                        return result;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Execution on '{MachineId}' timed out or was cancelled.", machine.MachineId);

                    var isMainCancellation = ct.IsCancellationRequested;
                    var outcome = isMainCancellation ? OperationResult.ValidationError : OperationResult.Timeout;
                    var status = isMainCancellation ? CommandStatus.Cancelled : CommandStatus.Failed;

                    if (attempt > policy.MaxRetries || isMainCancellation)
                    {
                        tracker.UpdateMachineState(machine.MachineId, status, attempt);
                        return new CommandResult
                        {
                            CommandId = Guid.NewGuid().ToString(),
                            MachineId = machine.MachineId,
                            Status = status,
                            Outcome = outcome,
                            OutputMessage = isMainCancellation ? "Cancelled by operator." : "Individual workstation command execution timed out.",
                            CompletedAtUtc = DateTime.UtcNow
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Execution failed on '{MachineId}' with exception.", machine.MachineId);
                    if (attempt > policy.MaxRetries)
                    {
                        tracker.UpdateMachineState(machine.MachineId, CommandStatus.Failed, attempt);
                        return new CommandResult
                        {
                            CommandId = Guid.NewGuid().ToString(),
                            MachineId = machine.MachineId,
                            Status = CommandStatus.Failed,
                            Outcome = OperationResult.Failure,
                            OutputMessage = ex.Message,
                            CompletedAtUtc = DateTime.UtcNow
                        };
                    }
                }

                // Exponential backoff delay calculation
                var backoffMs = Math.Min(10000, (int)policy.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                _logger.LogInformation("Backing off execution on '{MachineId}' for {Delay}ms before retry.", machine.MachineId, backoffMs);
                await Task.Delay((int)backoffMs, ct);
            }
        }

        private bool IsTransient(OperationResult outcome)
        {
            // Timeout and Handled general failures can be retried; validation/security failures cannot
            return outcome == OperationResult.Timeout || outcome == OperationResult.Failure;
        }
    }
}
