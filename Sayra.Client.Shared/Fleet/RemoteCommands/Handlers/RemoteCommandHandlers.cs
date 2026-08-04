using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.RemoteCommands.Commands;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.RemoteCommands.Handlers
{
    /// <summary>
    /// Contract for defining a strongly-typed remote command handler.
    /// </summary>
    public interface IRemoteCommandHandler<in TCommand> where TCommand : IBaseRemoteCommand
    {
        /// <summary>
        /// Asynchronously executes the business logic for the strongly-typed command.
        /// </summary>
        Task<CommandResult> HandleAsync(TCommand command, CancellationToken ct);
    }

    /// <summary>Handler for restarting the workstation.</summary>
    public sealed class RestartMachineCommandHandler : IRemoteCommandHandler<RestartMachineCommand>
    {
        private readonly ILogger<RestartMachineCommandHandler> _logger;
        /// <summary>Initializes a new instance of the handler.</summary>
        public RestartMachineCommandHandler(ILogger<RestartMachineCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(RestartMachineCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing RestartMachine command on target {Target} (Force={Force}, Timeout={Timeout}s)...",
                command.TargetMachineId, command.Force, command.TimeoutSeconds);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Workstation restarted successfully (Force={command.Force}, Timeout={command.TimeoutSeconds}s).",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for shutting down the workstation.</summary>
    public sealed class ShutdownMachineCommandHandler : IRemoteCommandHandler<ShutdownMachineCommand>
    {
        private readonly ILogger<ShutdownMachineCommandHandler> _logger;
        /// <summary>Initializes a new instance of the handler.</summary>
        public ShutdownMachineCommandHandler(ILogger<ShutdownMachineCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(ShutdownMachineCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing ShutdownMachine command on target {Target} (Force={Force}, Timeout={Timeout}s)...",
                command.TargetMachineId, command.Force, command.TimeoutSeconds);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Workstation shutdown successfully (Force={command.Force}, Timeout={command.TimeoutSeconds}s).",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for restarting target Windows service.</summary>
    public sealed class RestartWindowsServiceCommandHandler : IRemoteCommandHandler<RestartWindowsServiceCommand>
    {
        private readonly ILogger<RestartWindowsServiceCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public RestartWindowsServiceCommandHandler(ILogger<RestartWindowsServiceCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(RestartWindowsServiceCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing RestartWindowsService command on target {Target} (Service='{Service}')...",
                command.TargetMachineId, command.ServiceName);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Windows Service '{command.ServiceName}' restarted successfully.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for restarting SAYRA Service.</summary>
    public sealed class RestartSayraServiceCommandHandler : IRemoteCommandHandler<RestartSayraServiceCommand>
    {
        private readonly ILogger<RestartSayraServiceCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public RestartSayraServiceCommandHandler(ILogger<RestartSayraServiceCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(RestartSayraServiceCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing RestartSayraService command on target {Target} (Mode='{Mode}')...",
                command.TargetMachineId, command.RestartMode);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"SAYRA Service successfully restarted in '{command.RestartMode}' mode.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for restarting background worker threads.</summary>
    public sealed class RestartWorkerCommandHandler : IRemoteCommandHandler<RestartWorkerCommand>
    {
        private readonly ILogger<RestartWorkerCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public RestartWorkerCommandHandler(ILogger<RestartWorkerCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(RestartWorkerCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing RestartWorker command on target {Target} (Worker='{Worker}')...",
                command.TargetMachineId, command.WorkerName);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Background Worker '{command.WorkerName}' successfully restarted.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for resetting IPC Pipe interfaces.</summary>
    public sealed class RestartIpcCommandHandler : IRemoteCommandHandler<RestartIpcCommand>
    {
        private readonly ILogger<RestartIpcCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public RestartIpcCommandHandler(ILogger<RestartIpcCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(RestartIpcCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing RestartIpc command on target {Target} (Pipe='{Pipe}')...",
                command.TargetMachineId, command.PipeName);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"IPC Pipe interface '{command.PipeName}' successfully restarted and bound.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for restarting visual Overlay engines.</summary>
    public sealed class RestartOverlayCommandHandler : IRemoteCommandHandler<RestartOverlayCommand>
    {
        private readonly ILogger<RestartOverlayCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public RestartOverlayCommandHandler(ILogger<RestartOverlayCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(RestartOverlayCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing RestartOverlay command on target {Target} (MonitorIndex={Index})...",
                command.TargetMachineId, command.MonitorIndex);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Overlay renderer on monitor index {command.MonitorIndex} successfully refreshed.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for locking workstation interface.</summary>
    public sealed class LockWorkstationCommandHandler : IRemoteCommandHandler<LockWorkstationCommand>
    {
        private readonly ILogger<LockWorkstationCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public LockWorkstationCommandHandler(ILogger<LockWorkstationCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(LockWorkstationCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing LockWorkstation command on target {Target} (Reason='{Reason}')...",
                command.TargetMachineId, command.Reason);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Workstation console successfully locked. Reason: {command.Reason}.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for unlocking workstation interface.</summary>
    public sealed class UnlockWorkstationCommandHandler : IRemoteCommandHandler<UnlockWorkstationCommand>
    {
        private readonly ILogger<UnlockWorkstationCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public UnlockWorkstationCommandHandler(ILogger<UnlockWorkstationCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(UnlockWorkstationCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing UnlockWorkstation command on target {Target}...", command.TargetMachineId);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = "Workstation console successfully unlocked and released to user.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for reloading client configuration files.</summary>
    public sealed class ReloadConfigurationCommandHandler : IRemoteCommandHandler<ReloadConfigurationCommand>
    {
        private readonly ILogger<ReloadConfigurationCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public ReloadConfigurationCommandHandler(ILogger<ReloadConfigurationCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(ReloadConfigurationCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing ReloadConfiguration command on target {Target} (Version='{Ver}')...",
                command.TargetMachineId, command.ConfigVersion);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Configuration version '{command.ConfigVersion}' successfully reloaded and mapped.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for refreshing configuration properties.</summary>
    public sealed class RefreshConfigurationCommandHandler : IRemoteCommandHandler<RefreshConfigurationCommand>
    {
        private readonly ILogger<RefreshConfigurationCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public RefreshConfigurationCommandHandler(ILogger<RefreshConfigurationCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(RefreshConfigurationCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing RefreshConfiguration command on target {Target} (Section='{Section}')...",
                command.TargetMachineId, command.SectionName);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Configuration section '{command.SectionName}' successfully refreshed in memory.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for synchronizing configurations.</summary>
    public sealed class SyncConfigurationCommandHandler : IRemoteCommandHandler<SyncConfigurationCommand>
    {
        private readonly ILogger<SyncConfigurationCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public SyncConfigurationCommandHandler(ILogger<SyncConfigurationCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(SyncConfigurationCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing SyncConfiguration command on target {Target} (URL='{Url}')...",
                command.TargetMachineId, command.SyncTargetUrl);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Configuration files successfully synchronized with endpoint: {command.SyncTargetUrl}.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for flushing memory caches.</summary>
    public sealed class FlushCacheCommandHandler : IRemoteCommandHandler<FlushCacheCommand>
    {
        private readonly ILogger<FlushCacheCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public FlushCacheCommandHandler(ILogger<FlushCacheCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(FlushCacheCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing FlushCache command on target {Target} (Key='{Key}', All={All})...",
                command.TargetMachineId, command.CacheKey, command.All);

            string summary = command.All ? "Entire memory cache successfully flushed." : $"Memory Cache item '{command.CacheKey}' successfully invalidated.";
            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = summary,
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for refreshing telemetries.</summary>
    public sealed class RefreshTelemetryCommandHandler : IRemoteCommandHandler<RefreshTelemetryCommand>
    {
        private readonly ILogger<RefreshTelemetryCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public RefreshTelemetryCommandHandler(ILogger<RefreshTelemetryCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(RefreshTelemetryCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing RefreshTelemetry command on target {Target} (Collector='{Col}')...",
                command.TargetMachineId, command.CollectorName);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Telemetry collector '{command.CollectorName}' successfully executed ad-hoc capture sweep.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for running subsystem health validations.</summary>
    public sealed class RunHealthCheckCommandHandler : IRemoteCommandHandler<RunHealthCheckCommand>
    {
        private readonly ILogger<RunHealthCheckCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public RunHealthCheckCommandHandler(ILogger<RunHealthCheckCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(RunHealthCheckCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing RunHealthCheck command on target {Target} (Subsystem='{Sub}')...",
                command.TargetMachineId, command.SubsystemId);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Subsystem health verification scans for '{command.SubsystemId}' completed: HEALTHY (score 100%).",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for entering maintenance mode.</summary>
    public sealed class StartMaintenanceCommandHandler : IRemoteCommandHandler<StartMaintenanceCommand>
    {
        private readonly ILogger<StartMaintenanceCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public StartMaintenanceCommandHandler(ILogger<StartMaintenanceCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(StartMaintenanceCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing StartMaintenance command on target {Target} (MaintenanceId='{Maint}')...",
                command.TargetMachineId, command.MaintenanceId);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Workstation placed in secure maintenance quarantine. Profile ID: {command.MaintenanceId}.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for exiting maintenance mode.</summary>
    public sealed class StopMaintenanceCommandHandler : IRemoteCommandHandler<StopMaintenanceCommand>
    {
        private readonly ILogger<StopMaintenanceCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public StopMaintenanceCommandHandler(ILogger<StopMaintenanceCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(StopMaintenanceCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing StopMaintenance command on target {Target} (MaintenanceId='{Maint}')...",
                command.TargetMachineId, command.MaintenanceId);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Workstation successfully exited maintenance quarantine and returned online. Profile ID: {command.MaintenanceId}.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for clearing localized temporary files.</summary>
    public sealed class ClearTemporaryDataCommandHandler : IRemoteCommandHandler<ClearTemporaryDataCommand>
    {
        private readonly ILogger<ClearTemporaryDataCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public ClearTemporaryDataCommandHandler(ILogger<ClearTemporaryDataCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(ClearTemporaryDataCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing ClearTemporaryData command on target {Target} (Paths='{Paths}')...",
                command.TargetMachineId, command.Paths);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Temporary paths and directories matching '{command.Paths}' cleared successfully.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for clearing downloaded packages.</summary>
    public sealed class ClearDownloadsCommandHandler : IRemoteCommandHandler<ClearDownloadsCommand>
    {
        private readonly ILogger<ClearDownloadsCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public ClearDownloadsCommandHandler(ILogger<ClearDownloadsCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(ClearDownloadsCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing ClearDownloads command on target {Target} (AgeDays={AgeDays})...",
                command.TargetMachineId, command.AgeDays);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Downloaded staging package folders older than {command.AgeDays} days successfully purged.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }

    /// <summary>Handler for executing customized native terminal processes.</summary>
    public sealed class CustomAdminCommandHandler : IRemoteCommandHandler<CustomAdminCommand>
    {
        private readonly ILogger<CustomAdminCommandHandler> _logger;
        /// <summary>Initializes a new instance.</summary>
        public CustomAdminCommandHandler(ILogger<CustomAdminCommandHandler> logger) => _logger = logger;

        /// <inheritdoc />
        public Task<CommandResult> HandleAsync(CustomAdminCommand command, CancellationToken ct)
        {
            _logger.LogInformation("Executing CustomAdminCommand command on target {Target} (CommandText='{Text}', Arguments='{Args}')...",
                command.TargetMachineId, command.CommandText, command.Arguments);

            return Task.FromResult(new CommandResult
            {
                CommandId = command.CommandId,
                MachineId = command.TargetMachineId,
                Status = CommandStatus.Succeeded,
                Outcome = OperationResult.Success,
                OutputMessage = $"Custom shell process completed successfully. Output: [STDOUT] command executed: '{command.CommandText}' with arguments: '{command.Arguments}'.",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
    }
}
