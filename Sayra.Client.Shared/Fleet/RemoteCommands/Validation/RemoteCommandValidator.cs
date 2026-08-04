using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Fleet.RemoteCommands.Commands;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.RemoteCommands.Validation
{
    /// <summary>
    /// Performs comprehensive structural, parameter, machine availability, and version compatibility checks.
    /// Implementation of <see cref="IRemoteCommandValidator"/> for Phase 9.
    /// </summary>
    public sealed class RemoteCommandValidator : IRemoteCommandValidator
    {
        private readonly IFleetCache? _fleetCache;
        private readonly ILogger<RemoteCommandValidator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteCommandValidator"/> class.
        /// </summary>
        public RemoteCommandValidator(ILogger<RemoteCommandValidator> logger, IFleetCache? fleetCache = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fleetCache = fleetCache;
        }

        /// <inheritdoc />
        public Task<bool> ValidateCommandAsync(RemoteCommand command, CancellationToken ct = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            _logger.LogInformation("Performing structural and parameter validation for command {CommandId} ({Action})...",
                command.CommandId, command.Action);

            // 1. Validate Command Action Type
            var actionUpper = command.Action.ToUpperInvariant();
            var allowedActions = typeof(RemoteCommandActions).GetFields()
                .Where(f => f.IsLiteral && !f.IsInitOnly)
                .Select(f => f.GetRawConstantValue() as string)
                .ToList();

            if (!allowedActions.Contains(actionUpper))
            {
                _logger.LogError("Validation failed: Action verb '{Action}' is not supported in Phase 9.", command.Action);
                return Task.FromResult(false);
            }

            // 2. Validate Expiration
            if (command.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _logger.LogError("Validation failed: Command {CommandId} has expired. ExpiresAtUtc: {Expires}", command.CommandId, command.ExpiresAtUtc);
                return Task.FromResult(false);
            }

            // 3. Validate Target Machine Presence
            if (string.IsNullOrWhiteSpace(command.TargetMachineId))
            {
                _logger.LogError("Validation failed: TargetMachineId cannot be null or empty.");
                return Task.FromResult(false);
            }

            // 4. Validate Parameters
            if (!ValidateActionParameters(actionUpper, command))
            {
                return Task.FromResult(false);
            }

            // 5. Evaluate Workstation Availability and Version Compatibility via Fleet Cache (if available)
            if (_fleetCache != null)
            {
                var machine = _fleetCache.GetMachine(command.TargetMachineId);
                if (machine == null)
                {
                    _logger.LogWarning("Target workstation '{Target}' was not found in fleet cache. Skipping live state/version validation.", command.TargetMachineId);
                }
                else
                {
                    // State validation (MachineAvailability)
                    if (machine.Status == MachineStatus.Offline)
                    {
                        _logger.LogWarning("Target workstation '{Target}' is currently OFFLINE in fleet tracking. Command will be queued for offline replay.", command.TargetMachineId);
                        // We still allow validation to pass, as offline commands are enqueued in the offline queue!
                    }

                    // Version compatibility check
                    if (!string.IsNullOrEmpty(machine.Version?.SemVer))
                    {
                        if (machine.Version.SemVer.StartsWith("0."))
                        {
                            _logger.LogWarning("Target workstation '{Target}' is running pre-release version '{Version}'. Enforcing telemetry-only operations restrictions.", command.TargetMachineId, machine.Version.SemVer);

                            // Prevent custom admin command execution on very old legacy clients
                            if (actionUpper == RemoteCommandActions.CustomAdminCommand)
                            {
                                _logger.LogError("Validation failed: Custom Admin Command execution is blocked on target running legacy version '{Version}'.", machine.Version.SemVer);
                                return Task.FromResult(false);
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("Remote command {CommandId} successfully validated.", command.CommandId);
            return Task.FromResult(true);
        }

        private bool ValidateActionParameters(string actionUpper, RemoteCommand command)
        {
            var parameters = command.Parameters ?? new List<CommandParameter>();

            switch (actionUpper)
            {
                case RemoteCommandActions.RestartWindowsService:
                    var winService = parameters.FirstOrDefault(p => string.Equals(p.Name, "ServiceName", StringComparison.OrdinalIgnoreCase));
                    if (winService == null || string.IsNullOrWhiteSpace(winService.Value))
                    {
                        _logger.LogError("Validation failed: Action RESTART_WINDOWS_SERVICE requires a non-empty 'ServiceName' parameter.");
                        return false;
                    }
                    break;

                case RemoteCommandActions.RestartWorker:
                    var worker = parameters.FirstOrDefault(p => string.Equals(p.Name, "WorkerName", StringComparison.OrdinalIgnoreCase));
                    if (worker == null || string.IsNullOrWhiteSpace(worker.Value))
                    {
                        _logger.LogError("Validation failed: Action RESTART_WORKER requires a non-empty 'WorkerName' parameter.");
                        return false;
                    }
                    break;

                case RemoteCommandActions.RestartIpc:
                    var ipc = parameters.FirstOrDefault(p => string.Equals(p.Name, "PipeName", StringComparison.OrdinalIgnoreCase));
                    if (ipc == null || string.IsNullOrWhiteSpace(ipc.Value))
                    {
                        _logger.LogError("Validation failed: Action RESTART_IPC requires a non-empty 'PipeName' parameter.");
                        return false;
                    }
                    break;

                case RemoteCommandActions.LockWorkstation:
                    var reason = parameters.FirstOrDefault(p => string.Equals(p.Name, "Reason", StringComparison.OrdinalIgnoreCase));
                    if (reason == null || string.IsNullOrWhiteSpace(reason.Value))
                    {
                        _logger.LogError("Validation failed: Action LOCK_WORKSTATION requires a non-empty 'Reason' parameter.");
                        return false;
                    }
                    break;

                case RemoteCommandActions.UnlockWorkstation:
                    var code = parameters.FirstOrDefault(p => string.Equals(p.Name, "UnlockCode", StringComparison.OrdinalIgnoreCase));
                    if (code == null || string.IsNullOrWhiteSpace(code.Value))
                    {
                        _logger.LogError("Validation failed: Action UNLOCK_WORKSTATION requires a non-empty 'UnlockCode' parameter.");
                        return false;
                    }
                    break;

                case RemoteCommandActions.CustomAdminCommand:
                    var cmdText = parameters.FirstOrDefault(p => string.Equals(p.Name, "CommandText", StringComparison.OrdinalIgnoreCase));
                    if (cmdText == null || string.IsNullOrWhiteSpace(cmdText.Value))
                    {
                        _logger.LogError("Validation failed: Action CUSTOM_ADMIN_COMMAND requires a non-empty 'CommandText' parameter.");
                        return false;
                    }
                    break;
            }

            return true;
        }
    }
}
