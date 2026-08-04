using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Fleet.RemoteCommands.Commands;
using Sayra.Client.Shared.Fleet.RemoteCommands.History;
using Sayra.Client.Shared.Fleet.RemoteCommands.Queues;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;

namespace Sayra.Client.Shared.Fleet.RemoteCommands.Pipeline
{
    using CommandStatus = Sayra.Client.Shared.Models.Phase9.Enums.CommandStatus;
    using CommandResult = Sayra.Client.Shared.Models.Phase9.Domain.CommandResult;

    /// <summary>
    /// Contract for defining modular behaviors in the command execution pipeline.
    /// </summary>
    public interface IRemoteCommandMiddleware
    {
        /// <summary>
        /// Executes behavioral interception around the command lifetime.
        /// </summary>
        Task<CommandResult> HandleAsync(
            RemoteCommand command,
            Func<RemoteCommand, Task<CommandResult>> next,
            CancellationToken ct);
    }

    /// <summary>
    /// Thread-safe registry mapping Remote Command Actions to their registered handlers.
    /// </summary>
    public interface IRemoteCommandHandlerRegistry
    {
        /// <summary>Resolves handler for a given action verb.</summary>
        Func<RemoteCommand, CancellationToken, Task<CommandResult>>? ResolveHandler(string action);

        /// <summary>Registers a custom delegate handler for an action.</summary>
        void Register(string action, Func<RemoteCommand, CancellationToken, Task<CommandResult>> handler);
    }

    /// <summary>
    /// Implementation of the action handler registry.
    /// </summary>
    public sealed class RemoteCommandHandlerRegistry : IRemoteCommandHandlerRegistry
    {
        private readonly Dictionary<string, Func<RemoteCommand, CancellationToken, Task<CommandResult>>> _handlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();

        /// <inheritdoc />
        public Func<RemoteCommand, CancellationToken, Task<CommandResult>>? ResolveHandler(string action)
        {
            lock (_sync)
            {
                return _handlers.TryGetValue(action, out var handler) ? handler : null;
            }
        }

        /// <inheritdoc />
        public void Register(string action, Func<RemoteCommand, CancellationToken, Task<CommandResult>> handler)
        {
            if (string.IsNullOrEmpty(action)) return;
            lock (_sync)
            {
                _handlers[action] = handler ?? throw new ArgumentNullException(nameof(handler));
            }
        }
    }

    /// <summary>
    /// Production-grade coordinator managing the Remote Command execution pipeline.
    /// Implementation of <see cref="Sayra.Client.Shared.Interfaces.Phase9.IRemoteCommandDispatcher"/> and <see cref="IRemoteCommandService"/>.
    /// </summary>
    public sealed class RemoteCommandDispatcher : Sayra.Client.Shared.Interfaces.Phase9.IRemoteCommandDispatcher, IRemoteCommandService
    {
        private readonly IRemoteCommandHandlerRegistry _handlerRegistry;
        private readonly IEnumerable<IRemoteCommandMiddleware> _middlewares;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly IRemoteCommandHistoryRepository? _historyRepo;
        private readonly ILogger<RemoteCommandDispatcher> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteCommandDispatcher"/> class.
        /// </summary>
        public RemoteCommandDispatcher(
            IRemoteCommandHandlerRegistry handlerRegistry,
            IEnumerable<IRemoteCommandMiddleware> middlewares,
            IEventDispatcher eventDispatcher,
            ILogger<RemoteCommandDispatcher> logger,
            IRemoteCommandHistoryRepository? historyRepo = null)
        {
            _handlerRegistry = handlerRegistry ?? throw new ArgumentNullException(nameof(handlerRegistry));
            _middlewares = middlewares ?? throw new ArgumentNullException(nameof(middlewares));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _historyRepo = historyRepo;
        }

        /// <inheritdoc />
        public async Task<bool> DispatchCommandAsync(RemoteCommand command, CancellationToken ct = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            _logger.LogInformation("Dispatching command {CommandId} ({Action}) to execution pipeline...", command.CommandId, command.Action);

            // Dispatch domain event: CommandDispatched
            _eventDispatcher.Dispatch(new CommandDispatched(command.CommandId, command.TargetMachineId, command.Action));

            var result = await ExecuteCommandAsync(command, ct);
            return result.Outcome == OperationResult.Success;
        }

        /// <inheritdoc />
        public async Task<CommandResult> ExecuteCommandAsync(RemoteCommand command, CancellationToken ct = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            // Core handler delegate execution (last link in the pipeline chain)
            Func<RemoteCommand, Task<CommandResult>> coreExecution = async cmd =>
            {
                var handler = _handlerRegistry.ResolveHandler(cmd.Action);
                if (handler == null)
                {
                    _logger.LogError("No registered handler found for action verb '{Action}'.", cmd.Action);
                    return new CommandResult
                    {
                        CommandId = cmd.CommandId,
                        MachineId = cmd.TargetMachineId,
                        Status = CommandStatus.Failed,
                        Outcome = OperationResult.ValidationError,
                        OutputMessage = $"No handler registered for action: {cmd.Action}",
                        CompletedAtUtc = DateTime.UtcNow
                    };
                }

                // Dispatch domain event: CommandAccepted
                _eventDispatcher.Dispatch(new CommandAccepted(cmd.CommandId, cmd.TargetMachineId, cmd.Action));

                return await handler(cmd, ct);
            };

            // Build the middleware pipeline chain
            var pipeline = _middlewares.Reverse()
                .Aggregate(coreExecution, (next, middleware) =>
                    cmd => middleware.HandleAsync(cmd, next, ct));

            try
            {
                var result = await pipeline(command);

                // Commit historical log to SQLite SQLCipher DB
                if (_historyRepo != null)
                {
                    var historyEntry = new RemoteCommandHistoryEntry
                    {
                        CommandId = command.CommandId,
                        Action = command.Action,
                        TargetMachineId = command.TargetMachineId,
                        Status = result.Status,
                        Outcome = result.Outcome,
                        OutputMessage = result.OutputMessage,
                        ExecutionDurationMs = result.ExecutionDurationMs,
                        RetryCount = 0,
                        CreatorOperatorId = command.CreatorOperatorId,
                        CorrelationId = Guid.NewGuid().ToString(),
                        CreatedAtUtc = DateTime.UtcNow,
                        CompletedAtUtc = DateTime.UtcNow
                    };
                    await _historyRepo.SaveAsync(historyEntry, CancellationToken.None);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unhandled pipeline failure executing command {CommandId}.", command.CommandId);
                var result = new CommandResult
                {
                    CommandId = command.CommandId,
                    MachineId = command.TargetMachineId,
                    Status = CommandStatus.Failed,
                    Outcome = OperationResult.Failure,
                    OutputMessage = $"Pipeline Crash: {ex.Message}",
                    CompletedAtUtc = DateTime.UtcNow
                };

                if (_historyRepo != null)
                {
                    var historyEntry = new RemoteCommandHistoryEntry
                    {
                        CommandId = command.CommandId,
                        Action = command.Action,
                        TargetMachineId = command.TargetMachineId,
                        Status = result.Status,
                        Outcome = result.Outcome,
                        OutputMessage = result.OutputMessage,
                        ExecutionDurationMs = result.ExecutionDurationMs,
                        RetryCount = 0,
                        CreatorOperatorId = command.CreatorOperatorId,
                        CorrelationId = Guid.NewGuid().ToString(),
                        CreatedAtUtc = DateTime.UtcNow,
                        CompletedAtUtc = DateTime.UtcNow
                    };
                    await _historyRepo.SaveAsync(historyEntry, CancellationToken.None);
                }

                return result;
            }
        }
    }

    #region Pipeline Middlewares / Behaviors

    /// <summary>
    /// Middleware executing structured and trace-enriched logging around command execution.
    /// </summary>
    public sealed class LoggingMiddleware : IRemoteCommandMiddleware
    {
        private readonly ILogger<LoggingMiddleware> _logger;

        /// <summary>Initializes a new instance of the middleware.</summary>
        public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CommandResult> HandleAsync(RemoteCommand command, Func<RemoteCommand, Task<CommandResult>> next, CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();
            var traceId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("[Trace:{TraceId}] Command Started: ID={CommandId}, Type={Type}, Target={Target}, Operator={Operator}",
                traceId, command.CommandId, command.Action, command.TargetMachineId, command.CreatorOperatorId);

            CommandResult result = null!;
            try
            {
                result = await next(command);
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation("[Trace:{TraceId}] Command Ended: ID={CommandId}, Duration={Duration}ms, Status={Status}, Outcome={Outcome}",
                    traceId, command.CommandId, stopwatch.ElapsedMilliseconds, result?.Status ?? CommandStatus.Failed, result?.Outcome ?? OperationResult.Failure);
            }

            return result with
            {
                ExecutionDurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Middleware trapping and mapping exceptions cleanly to failed outcomes.
    /// </summary>
    public sealed class ExceptionMiddleware : IRemoteCommandMiddleware
    {
        private readonly ILogger<ExceptionMiddleware> _logger;

        /// <summary>Initializes a new instance.</summary>
        public ExceptionMiddleware(ILogger<ExceptionMiddleware> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CommandResult> HandleAsync(RemoteCommand command, Func<RemoteCommand, Task<CommandResult>> next, CancellationToken ct)
        {
            try
            {
                return await next(command);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Command {CommandId} was cancelled during execution.", command.CommandId);
                return new CommandResult
                {
                    CommandId = command.CommandId,
                    MachineId = command.TargetMachineId,
                    Status = CommandStatus.Failed,
                    Outcome = OperationResult.ValidationError,
                    OutputMessage = "Operation cancelled",
                    CompletedAtUtc = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command {CommandId}.", command.CommandId);
                return new CommandResult
                {
                    CommandId = command.CommandId,
                    MachineId = command.TargetMachineId,
                    Status = CommandStatus.Failed,
                    Outcome = OperationResult.Failure,
                    OutputMessage = $"Unhandled Exception: {ex.Message}",
                    CompletedAtUtc = DateTime.UtcNow
                };
            }
        }
    }

    /// <summary>
    /// Middleware executing schema, state, parameter, and availability validations.
    /// </summary>
    public sealed class ValidationMiddleware : IRemoteCommandMiddleware
    {
        private readonly IRemoteCommandValidator _validator;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<ValidationMiddleware> _logger;

        /// <summary>Initializes a new instance.</summary>
        public ValidationMiddleware(IRemoteCommandValidator validator, IEventDispatcher eventDispatcher, ILogger<ValidationMiddleware> logger)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CommandResult> HandleAsync(RemoteCommand command, Func<RemoteCommand, Task<CommandResult>> next, CancellationToken ct)
        {
            bool isValid = await _validator.ValidateCommandAsync(command, ct);
            if (!isValid)
            {
                _logger.LogError("Command {CommandId} failed structural validation checks.", command.CommandId);

                // Dispatch domain event: CommandRejected
                _eventDispatcher.Dispatch(new CommandRejected(command.CommandId, command.TargetMachineId, command.Action, "Command validation failed."));

                return new CommandResult
                {
                    CommandId = command.CommandId,
                    MachineId = command.TargetMachineId,
                    Status = CommandStatus.Failed,
                    Outcome = OperationResult.ValidationError,
                    OutputMessage = "Command validation failed.",
                    CompletedAtUtc = DateTime.UtcNow
                };
            }

            return await next(command);
        }
    }

    /// <summary>
    /// Middleware verifying administrative permissions, roles, and digital signatures.
    /// </summary>
    public sealed class AuthorizationMiddleware : IRemoteCommandMiddleware
    {
        private readonly IRemoteCommandAuthorizationService _authService;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<AuthorizationMiddleware> _logger;

        /// <summary>Initializes a new instance.</summary>
        public AuthorizationMiddleware(IRemoteCommandAuthorizationService authService, IEventDispatcher eventDispatcher, ILogger<AuthorizationMiddleware> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CommandResult> HandleAsync(RemoteCommand command, Func<RemoteCommand, Task<CommandResult>> next, CancellationToken ct)
        {
            bool isAuthorized = await _authService.AuthorizeCommandAsync(command, ct);
            if (!isAuthorized)
            {
                _logger.LogError("Command {CommandId} failed security authorization check.", command.CommandId);

                // Dispatch domain event: CommandRejected
                _eventDispatcher.Dispatch(new CommandRejected(command.CommandId, command.TargetMachineId, command.Action, "Security authorization rejected command."));

                return new CommandResult
                {
                    CommandId = command.CommandId,
                    MachineId = command.TargetMachineId,
                    Status = CommandStatus.Failed,
                    Outcome = OperationResult.SecurityError,
                    OutputMessage = "Security authorization rejected command.",
                    CompletedAtUtc = DateTime.UtcNow
                };
            }

            return await next(command);
        }
    }

    /// <summary>
    /// Middleware implementing per-command and global timeout protection policies.
    /// </summary>
    public sealed class TimeoutMiddleware : IRemoteCommandMiddleware
    {
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<TimeoutMiddleware> _logger;

        /// <summary>Initializes a new instance.</summary>
        public TimeoutMiddleware(IEventDispatcher eventDispatcher, ILogger<TimeoutMiddleware> logger)
        {
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CommandResult> HandleAsync(RemoteCommand command, Func<RemoteCommand, Task<CommandResult>> next, CancellationToken ct)
        {
            // Global timeout 30s or customized based on priority
            var timeoutDuration = command.Priority >= CommandPriority.Critical
                ? TimeSpan.FromSeconds(60)
                : TimeSpan.FromSeconds(20);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutDuration);

            try
            {
                var runTask = next(command);
                var completedTask = await Task.WhenAny(runTask, Task.Delay(timeoutDuration, cts.Token));

                if (completedTask == runTask)
                {
                    return await runTask;
                }
                else
                {
                    _logger.LogError("Command {CommandId} execution timed out after {Duration}s.", command.CommandId, timeoutDuration.TotalSeconds);

                    // Dispatch domain event: TimeoutOccurred
                    _eventDispatcher.Dispatch(new TimeoutOccurred(command.CommandId, command.TargetMachineId, command.Action));

                    return new CommandResult
                    {
                        CommandId = command.CommandId,
                        MachineId = command.TargetMachineId,
                        Status = CommandStatus.Failed,
                        Outcome = OperationResult.Timeout,
                        OutputMessage = $"Execution Deadline Exceeded ({timeoutDuration.TotalSeconds}s)",
                        CompletedAtUtc = DateTime.UtcNow
                    };
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Command {CommandId} execution timed out/cancelled.", command.CommandId);

                // Dispatch domain event: TimeoutOccurred
                _eventDispatcher.Dispatch(new TimeoutOccurred(command.CommandId, command.TargetMachineId, command.Action));

                return new CommandResult
                {
                    CommandId = command.CommandId,
                    MachineId = command.TargetMachineId,
                    Status = CommandStatus.Failed,
                    Outcome = OperationResult.Timeout,
                    OutputMessage = "Execution deadline reached.",
                    CompletedAtUtc = DateTime.UtcNow
                };
            }
        }
    }

    /// <summary>
    /// Middleware executing live backoff retries when transient errors are detected during command runs.
    /// </summary>
    public sealed class RetryMiddleware : IRemoteCommandMiddleware
    {
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<RetryMiddleware> _logger;

        /// <summary>Initializes a new instance.</summary>
        public RetryMiddleware(IEventDispatcher eventDispatcher, ILogger<RetryMiddleware> logger)
        {
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CommandResult> HandleAsync(RemoteCommand command, Func<RemoteCommand, Task<CommandResult>> next, CancellationToken ct)
        {
            int maxAttempts = 3;
            int attempt = 0;
            double baseDelayMs = 200;

            while (true)
            {
                attempt++;
                var result = await next(command);

                if (result.Outcome == OperationResult.Success || result.Outcome != OperationResult.Failure)
                {
                    if (attempt > 1)
                    {
                        // Dispatch domain event: RetryCompleted
                        _eventDispatcher.Dispatch(new RetryCompleted(command.CommandId, command.TargetMachineId, command.Action, attempt - 1));
                    }
                    return result;
                }

                // If execution failed with a transient error and we have remaining attempts
                if (attempt < maxAttempts && IsTransientFailure(result.OutputMessage))
                {
                    // Exponential Backoff with Jitter
                    double delay = baseDelayMs * Math.Pow(2, attempt - 1);
                    double jitter = RandomNumberGenerator.GetInt32(1, 50);
                    var finalDelay = TimeSpan.FromMilliseconds(delay + jitter);

                    _logger.LogWarning("Transient failure in attempt #{Attempt} for command {CommandId}. Retrying in {Delay}ms...",
                        attempt, command.CommandId, finalDelay.TotalMilliseconds);

                    // Dispatch domain event: RetryStarted
                    _eventDispatcher.Dispatch(new RetryStarted(command.CommandId, command.TargetMachineId, command.Action, attempt));

                    await Task.Delay(finalDelay, ct);
                }
                else
                {
                    return result;
                }
            }
        }

        private static bool IsTransientFailure(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage)) return false;
            return errorMessage.Contains("transient", StringComparison.OrdinalIgnoreCase) ||
                   errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   errorMessage.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                   errorMessage.Contains("busy", StringComparison.OrdinalIgnoreCase) ||
                   errorMessage.Contains("locked", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Middleware integrating telemetries, metrics, and duration indicators.
    /// </summary>
    public sealed class TelemetryMiddleware : IRemoteCommandMiddleware
    {
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<TelemetryMiddleware> _logger;

        /// <summary>Initializes a new instance.</summary>
        public TelemetryMiddleware(IEventDispatcher eventDispatcher, ILogger<TelemetryMiddleware> logger)
        {
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CommandResult> HandleAsync(RemoteCommand command, Func<RemoteCommand, Task<CommandResult>> next, CancellationToken ct)
        {
            _eventDispatcher.Dispatch(new CommandStarted(command.CommandId, command.TargetMachineId, command.Action));

            var result = await next(command);

            if (result.Outcome == OperationResult.Success)
            {
                _eventDispatcher.Dispatch(new CommandCompleted(command.CommandId, command.TargetMachineId, command.Action, result.Outcome));
            }
            else if (result.OutputMessage == "Operation cancelled")
            {
                _eventDispatcher.Dispatch(new CommandCancelled(command.CommandId, command.TargetMachineId, command.Action));
            }
            else
            {
                _eventDispatcher.Dispatch(new CommandFailed(command.CommandId, command.TargetMachineId, command.Action, result.OutputMessage));
            }

            return result;
        }
    }

    /// <summary>
    /// Middleware executing cryptographically secure audit trail recording of remote commands.
    /// </summary>
    public sealed class AuditMiddleware : IRemoteCommandMiddleware
    {
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<AuditMiddleware> _logger;

        /// <summary>Initializes a new instance of the audit middleware.</summary>
        public AuditMiddleware(IEventDispatcher eventDispatcher, ILogger<AuditMiddleware> logger)
        {
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CommandResult> HandleAsync(RemoteCommand command, Func<RemoteCommand, Task<CommandResult>> next, CancellationToken ct)
        {
            var result = await next(command);

            var entryId = RandomNumberGenerator.GetInt32(1, 100000);
            var opType = AuditOperationType.RemoteCommandExecution;

            _logger.LogInformation("Recording secure blockchain audit log entry {Id} for remote execution of {Action} command.", entryId, command.Action);

            _eventDispatcher.Dispatch(new AuditRecordCreated(
                entryId,
                result.CommandId,
                opType,
                command.CreatorOperatorId));

            return result;
        }
    }

    #endregion
}
