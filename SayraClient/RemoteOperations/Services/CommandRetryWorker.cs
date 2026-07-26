using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using SayraClient.Services;

namespace SayraClient.RemoteOperations.Services
{
    public class CommandRetryWorker : SupervisedBackgroundService
    {
        private readonly IRemoteCommandRepository _repository;
        private readonly IDeadLetterQueue _dlq;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
        private readonly int _maxRetryCount;

        public CommandRetryWorker(
            ILogger<CommandRetryWorker> logger,
            IServiceHealthMonitor healthMonitor,
            IRemoteCommandRepository repository,
            IDeadLetterQueue dlq,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
            : base(logger, healthMonitor, "CommandRetryWorker")
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _dlq = dlq ?? throw new ArgumentNullException(nameof(dlq));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Configurable maximum retry count, defaulting to 4 attempts (Retry 1-4)
            _maxRetryCount = _configuration.GetValue<int>("RemoteCommands:MaxRetryCount", 4);
        }

        public static TimeSpan GetBackoffDelay(int attempt)
        {
            return attempt switch
            {
                1 => TimeSpan.FromSeconds(5),
                2 => TimeSpan.FromSeconds(30),
                3 => TimeSpan.FromMinutes(5),
                4 => TimeSpan.FromMinutes(30),
                _ => TimeSpan.FromMinutes(30)
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CommandRetryWorker execution loop starting. Poll interval: {Interval}s. Max retries: {MaxRetries}",
                _pollInterval.TotalSeconds, _maxRetryCount);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Scan history for FAILED commands
                    var history = await _repository.GetHistoryAsync(stoppingToken);
                    var failedCommands = new List<RemoteCommandHistory>();

                    foreach (var h in history)
                    {
                        if (string.Equals(h.Status, "FAILED", StringComparison.OrdinalIgnoreCase))
                        {
                            failedCommands.Add(h);
                        }
                    }

                    if (failedCommands.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} failed commands in history. Evaluating for retries...", failedCommands.Count);

                        foreach (var command in failedCommands)
                        {
                            if (stoppingToken.IsCancellationRequested) break;

                            await ProcessFailedCommandAsync(command, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during command retry execution cycle.");
                }

                try
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("CommandRetryWorker execution loop stopped.");
        }

        private async Task ProcessFailedCommandAsync(RemoteCommandHistory command, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(command.CompletedAt) || !DateTime.TryParse(command.CompletedAt, out var completedAt))
                {
                    _logger.LogWarning("Command {CommandId} has invalid CompletedAt timestamp. Resetting CompletedAt to Now.", command.CommandId);
                    command.CompletedAt = DateTime.UtcNow.ToString("O");
                    await _repository.SaveCommandAsync(command, cancellationToken);
                    return;
                }

                int nextAttempt = command.RetryCount + 1;
                var backoff = GetBackoffDelay(nextAttempt);
                var triggerTime = completedAt.Add(backoff);

                if (DateTime.UtcNow >= triggerTime)
                {
                    if (nextAttempt > _maxRetryCount)
                    {
                        _logger.LogWarning("Command {CommandId} has exceeded max retries ({Max}). Routing to DLQ.", command.CommandId, _maxRetryCount);
                        await _dlq.MoveToDeadLetterAsync(command, $"Exceeded maximum retries of {_maxRetryCount}.", command.RetryCount, cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation("Retrying command {CommandId} ({Action}). Attempt {Attempt} of {Max}.",
                            command.CommandId, command.Action, nextAttempt, _maxRetryCount);

                        // Increment RetryCount and reset status to PENDING
                        command.RetryCount = nextAttempt;
                        command.Status = "PENDING";
                        command.ErrorMessage = null;
                        command.StartedAt = null;
                        command.CompletedAt = null;
                        command.ExecutionDurationMs = null;

                        await _repository.SaveCommandAsync(command, cancellationToken);

                        // Re-queue to the engine
                        var engine = _serviceProvider.GetService<IRemoteCommandEngine>();
                        if (engine != null)
                        {
                            var rc = new RemoteCommand
                            {
                                CommandId = Guid.Parse(command.CommandId),
                                Action = command.Action,
                                TargetClientId = command.TargetPcId,
                                SenderAdminId = command.SenderAdminId,
                                Payload = command.PayloadJson ?? string.Empty,
                                Status = CommandStatus.Pending,
                                Signature = command.Signature,
                                Timestamp = DateTime.Parse(command.ReceivedAt),
                                ExpirationTime = DateTime.Parse(command.ReceivedAt).AddMinutes(5)
                            };

                            await engine.QueueCommandAsync(rc);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process retry for command {CommandId}.", command.CommandId);
            }
        }
    }
}
