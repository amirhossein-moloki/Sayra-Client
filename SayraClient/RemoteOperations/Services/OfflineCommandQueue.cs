using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class OfflineCommandQueue : IOfflineCommandQueue
    {
        private readonly IRemoteCommandRepository _repository;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OfflineCommandQueue> _logger;

        public OfflineCommandQueue(
            IRemoteCommandRepository repository,
            IServiceProvider serviceProvider,
            ILogger<OfflineCommandQueue> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task EnqueueOfflineCommandAsync(RemoteCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            _logger.LogInformation("Enqueuing offline command {CommandId} ({Action}) to secure database.",
                command.CommandId, command.Action);

            var history = new RemoteCommandHistory
            {
                CommandId = command.CommandId.ToString(),
                Action = command.Action,
                TargetPcId = command.TargetClientId,
                SenderAdminId = command.SenderAdminId,
                PayloadJson = command.Payload,
                Status = "PENDING",
                ReceivedAt = command.Timestamp.ToString("O"),
                Signature = command.Signature
            };

            await _repository.SaveCommandAsync(history, cancellationToken);

            // Queue to local engine for immediate processing if engine is registered and active
            var engine = _serviceProvider.GetService<IRemoteCommandEngine>();
            if (engine != null)
            {
                await engine.QueueCommandAsync(command);
            }
        }

        public async Task RestoreAndResumeQueueAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Restoring and resuming pending remote commands from offline queue...");

            // Get pending commands from DB
            var pendingHistory = await _repository.GetPendingCommandsAsync(cancellationToken);

            // Also search for any "EXECUTING" commands that got interrupted (e.g., application crash or power loss)
            var allHistory = await _repository.GetHistoryAsync(cancellationToken);
            var interruptedHistory = new List<RemoteCommandHistory>();
            foreach (var h in allHistory)
            {
                if (string.Equals(h.Status, "EXECUTING", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Found interrupted executing command {CommandId} ({Action}). Rescheduling...", h.CommandId, h.Action);
                    // Reset status to PENDING in the DB
                    await _repository.UpdateStatusAsync(h.CommandId, "PENDING", "Interrupted during execution.", cancellationToken);
                    h.Status = "PENDING";
                    interruptedHistory.Add(h);
                }
            }

            // Combine both sets
            var commandsToRestore = new List<RemoteCommandHistory>(pendingHistory);
            foreach (var ih in interruptedHistory)
            {
                if (!commandsToRestore.Exists(c => c.CommandId == ih.CommandId))
                {
                    commandsToRestore.Add(ih);
                }
            }

            // Sort by ReceivedAt ASC to preserve ordering
            commandsToRestore.Sort((a, b) => string.Compare(a.ReceivedAt, b.ReceivedAt, StringComparison.Ordinal));

            var engine = _serviceProvider.GetService<IRemoteCommandEngine>();
            if (engine == null)
            {
                _logger.LogWarning("RemoteCommandEngine is not available in ServiceProvider. Cannot resume execution.");
                return;
            }

            foreach (var h in commandsToRestore)
            {
                _logger.LogInformation("Restoring command {CommandId} ({Action}) to execution engine.", h.CommandId, h.Action);

                var rc = new RemoteCommand
                {
                    CommandId = Guid.Parse(h.CommandId),
                    Action = h.Action,
                    TargetClientId = h.TargetPcId,
                    SenderAdminId = h.SenderAdminId,
                    Payload = h.PayloadJson ?? string.Empty,
                    Status = CommandStatus.Pending,
                    Signature = h.Signature,
                    Timestamp = DateTime.Parse(h.ReceivedAt),
                    ExpirationTime = DateTime.Parse(h.ReceivedAt).AddMinutes(5)
                };

                await engine.QueueCommandAsync(rc);
            }

            _logger.LogInformation("Restored {Count} commands from the offline queue.", commandsToRestore.Count);
        }
    }
}
