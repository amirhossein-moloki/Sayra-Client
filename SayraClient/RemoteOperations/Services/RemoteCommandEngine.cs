using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly CancellationTokenSource _cts = new();

        public RemoteCommandEngine(
            ILogger<RemoteCommandEngine> logger,
            IServiceHealthMonitor healthMonitor,
            IRemoteCommandDispatcher dispatcher,
            ICommandResultReporter resultReporter)
            : base(logger, healthMonitor, "RemoteCommandEngine")
        {
            _dispatcher = dispatcher;
            _resultReporter = resultReporter;
        }

        public Task StartEngineAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Remote Command Engine started manual trigger.");
            return Task.CompletedTask;
        }

        public Task QueueCommandAsync(RemoteCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            _logger.LogInformation("Queueing remote command {CommandId} ({Action}) with priority {Priority}.",
                command.CommandId, command.Action, command.Priority);

            command.Status = CommandStatus.Pending;
            _queue.Enqueue(command);
            _resultReporter.SendStatusUpdateAsync(command.CommandId, CommandStatus.Pending);
            return Task.CompletedTask;
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

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _cts.Token);

            while (!linkedCts.Token.IsCancellationRequested)
            {
                RemoteCommand? command = null;
                try
                {
                    command = await _queue.DequeueAsync(linkedCts.Token);

                    // Step A: Update Status to Validating / Executing
                    command.Status = CommandStatus.Executing;
                    await _resultReporter.SendStatusUpdateAsync(command.CommandId, CommandStatus.Executing);

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
                    }
                }
            }

            _logger.LogInformation("Remote Command Engine execution loop stopped.");
        }
    }
}
