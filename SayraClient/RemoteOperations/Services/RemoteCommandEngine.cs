using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using SayraClient.Services;

namespace SayraClient.RemoteOperations.Services
{
    public class PriorityCommandQueue
    {
        private readonly PriorityQueue<RemoteCommand, int> _queue = new();
        private readonly SemaphoreSlim _semaphore = new(0);
        private readonly object _lock = new();

        public void Enqueue(RemoteCommand command)
        {
            // Lower priority values are executed first in standard .NET PriorityQueue.
            // Map Priority to an integer priority value: High -> 1, Normal -> 2, Low -> 3
            int priorityValue = command.Priority?.ToUpperInvariant() switch
            {
                "HIGH" => 1,
                "NORMAL" => 2,
                "LOW" => 3,
                _ => 2
            };

            lock (_lock)
            {
                _queue.Enqueue(command, priorityValue);
            }
            _semaphore.Release();
        }

        public async Task<RemoteCommand> DequeueAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            lock (_lock)
            {
                return _queue.Dequeue();
            }
        }

        public int Count
        {
            get
            {
                lock (_lock) return _queue.Count;
            }
        }
    }

    public class RemoteCommandEngine : SupervisedBackgroundService, IRemoteCommandEngine
    {
        private readonly PriorityCommandQueue _queue = new();
        private readonly IRemoteCommandDispatcher _dispatcher;
        private readonly ICommandResultReporter _resultReporter;
        private readonly ILocalDatabaseService _databaseService;
        private readonly IRemoteCommandRepository _repository;
        private readonly IAuditService _auditService;
        private readonly IServiceProvider _serviceProvider;
        private readonly CancellationTokenSource _cts = new();

        public RemoteCommandEngine(
            ILogger<RemoteCommandEngine> logger,
            IServiceHealthMonitor healthMonitor,
            IRemoteCommandDispatcher dispatcher,
            ICommandResultReporter resultReporter,
            ILocalDatabaseService databaseService,
            IRemoteCommandRepository repository,
            IAuditService auditService,
            IServiceProvider serviceProvider)
            : base(logger, healthMonitor, "RemoteCommandEngine")
        {
            _dispatcher = dispatcher;
            _resultReporter = resultReporter;
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public Task StartEngineAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Remote Command Engine started manual trigger.");
            return Task.CompletedTask;
        }

        public async Task QueueCommandAsync(RemoteCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            _logger.LogInformation("Queueing remote command {CommandId} ({Action}) with priority {Priority}.",
                command.CommandId, command.Action, command.Priority);

            command.Status = CommandStatus.Pending;

            // Ensure database is initialized before saving
            try
            {
                await _databaseService.InitializeDatabaseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure database initialization on manual queue command.");
            }

            // Persist the transition to PENDING in the local database
            var history = new RemoteCommandHistory
            {
                CommandId = command.CommandId.ToString(),
                Action = command.Action,
                TargetPcId = command.TargetClientId,
                SenderAdminId = command.SenderAdminId,
                PayloadJson = command.Payload,
                Status = "PENDING",
                ReceivedAt = command.Timestamp.ToString("O"),
                Signature = command.Signature,
                RetryCount = 0
            };
            await _repository.SaveCommandAsync(history);

            // Record audit log
            await _auditService.RecordCommandReceivedAsync(command.CommandId.ToString(), command.Action, command.CommandId.ToString());

            _queue.Enqueue(command);
            await _resultReporter.SendStatusUpdateAsync(command.CommandId, CommandStatus.Pending);
        }

        public Task<CommandStatus> GetCommandStatusAsync(Guid commandId)
        {
            if (_resultReporter is CommandResultReporter crr)
            {
                return Task.FromResult(crr.GetCachedStatus(commandId));
            }
            return Task.FromResult(CommandStatus.Pending);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Remote Command Engine execution loop starting...");

            // Initialize local secure database
            try
            {
                await _databaseService.InitializeDatabaseAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize local secure database upon startup.");
            }

            // Load offline queue on startup
            try
            {
                var offlineQueue = _serviceProvider.GetService<IOfflineCommandQueue>();
                if (offlineQueue != null)
                {
                    await offlineQueue.RestoreAndResumeQueueAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring offline queue during engine startup.");
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _cts.Token);

            while (!linkedCts.Token.IsCancellationRequested)
            {
                RemoteCommand? command = null;
                try
                {
                    command = await _queue.DequeueAsync(linkedCts.Token);

                    // Step A: Update Status to EXECUTING in memory
                    command.Status = CommandStatus.Executing;
                    await _resultReporter.SendStatusUpdateAsync(command.CommandId, CommandStatus.Executing);

                    // Persist the transition to EXECUTING in the database
                    await _repository.UpdateStatusAsync(command.CommandId.ToString(), "EXECUTING", cancellationToken: linkedCts.Token);

                    // Record audit log
                    await _auditService.RecordExecutionStartedAsync(command.CommandId.ToString(), command.Action, command.CommandId.ToString(), linkedCts.Token);

                    _logger.LogInformation("Remote Command Engine processing command {CommandId} ({Action}).",
                        command.CommandId, command.Action);

                    var stopwatch = Stopwatch.StartNew();

                    // Step B: Error Isolation during command execution
                    CommandResult result;
                    try
                    {
                        result = await _dispatcher.DispatchAsync(command, linkedCts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Command handler crashed for action {Action}.", command.Action);
                        result = CommandResult.Failed(command.CommandId, "HANDLER_ERROR", ex.Message);
                    }

                    stopwatch.Stop();
                    result.ExecutionTime = stopwatch.Elapsed;

                    // Step C: Update final status
                    command.Status = result.Success ? CommandStatus.Completed : CommandStatus.Failed;
                    await _resultReporter.ReportResultAsync(result);
                    await _resultReporter.SendStatusUpdateAsync(command.CommandId, command.Status);

                    // Persist the final transition (COMPLETED / FAILED) in the database
                    string finalStatusStr = result.Success ? "COMPLETED" : "FAILED";
                    await _repository.UpdateStatusAsync(command.CommandId.ToString(), finalStatusStr, result.Success ? null : result.ErrorMessage, linkedCts.Token);

                    // Record final audit log
                    if (result.Success)
                    {
                        await _auditService.RecordExecutionCompletedAsync(command.CommandId.ToString(), command.Action, command.CommandId.ToString(), linkedCts.Token);
                    }
                    else
                    {
                        await _auditService.RecordExecutionFailedAsync(command.CommandId.ToString(), command.Action, result.ErrorMessage, command.CommandId.ToString(), linkedCts.Token);
                    }
                }
                catch (OperationCanceledException) when (linkedCts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning("Remote Command Engine execution loop cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in Remote Command Engine processing loop.");
                    if (command != null)
                    {
                        command.Status = CommandStatus.Failed;
                        await _resultReporter.SendStatusUpdateAsync(command.CommandId, CommandStatus.Failed);
                        try
                        {
                            await _repository.UpdateStatusAsync(command.CommandId.ToString(), "FAILED", ex.Message, linkedCts.Token);
                            await _auditService.RecordExecutionFailedAsync(command.CommandId.ToString(), command.Action, ex.Message, command.CommandId.ToString(), linkedCts.Token);
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogError(dbEx, "Failed to persist fallback failure for command {CommandId}.", command.CommandId);
                        }
                    }
                }
            }

            _logger.LogInformation("Remote Command Engine execution loop stopped.");
        }
    }
}
