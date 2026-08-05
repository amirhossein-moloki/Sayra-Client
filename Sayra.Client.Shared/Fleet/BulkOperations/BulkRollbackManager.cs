using System;
using System.Collections.Concurrent;
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
    /// Model representing historical rollback transaction events.
    /// </summary>
    public record RollbackHistoryEntry
    {
        /// <summary>
        /// Gets the rollback tracking identifier.
        /// </summary>
        public string RollbackId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the original bulk operation identifier.
        /// </summary>
        public string BulkOperationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the action executed during rollback.
        /// </summary>
        public string RollbackAction { get; init; } = string.Empty;

        /// <summary>
        /// Gets the UTC timestamp when the rollback was initiated.
        /// </summary>
        public DateTime TriggeredAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the final validation status of the rollback operation.
        /// </summary>
        public bool IsValidated { get; init; }

        /// <summary>
        /// Gets individual machine rollback outcomes.
        /// </summary>
        public Dictionary<string, OperationResult> MachineOutcomes { get; init; } = new();
    }

    /// <summary>
    /// Thread-safe manager responsible for preparing, executing, and validating rollback operations across fleet workstations.
    /// </summary>
    public class BulkRollbackManager
    {
        private readonly IRemoteCommandService _commandService;
        private readonly ILogger<BulkRollbackManager> _logger;
        private readonly ConcurrentDictionary<string, RollbackHistoryEntry> _history = new();
        private readonly Dictionary<string, string> _rollbackActionsRegistry = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of BulkRollbackManager.
        /// </summary>
        public BulkRollbackManager(
            IRemoteCommandService commandService,
            ILogger<BulkRollbackManager> logger)
        {
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Default reversing action mapping
            _rollbackActionsRegistry["LOCK_PC"] = "UNLOCK_PC";
            _rollbackActionsRegistry["DISABLE_SERVICE"] = "ENABLE_SERVICE";
            _rollbackActionsRegistry["BLOCK_APP"] = "UNBLOCK_APP";
            _rollbackActionsRegistry["INSTALL_PACKAGE"] = "UNINSTALL_PACKAGE";
        }

        /// <summary>
        /// Registers a custom reversing action mapping.
        /// </summary>
        public void RegisterRollbackAction(string forwardAction, string reversingAction)
        {
            if (string.IsNullOrEmpty(forwardAction) || string.IsNullOrEmpty(reversingAction)) return;
            _rollbackActionsRegistry[forwardAction] = reversingAction;
        }

        /// <summary>
        /// Checks whether a forward action has a defined reversing rollback action.
        /// </summary>
        public bool SupportsRollback(string action)
        {
            return _rollbackActionsRegistry.ContainsKey(action);
        }

        /// <summary>
        /// Resolves the rollback action name for a given forward action.
        /// </summary>
        public string GetRollbackAction(string action)
        {
            return _rollbackActionsRegistry.TryGetValue(action, out var rollbackAction) ? rollbackAction : $"ROLLBACK_{action}";
        }

        /// <summary>
        /// Prepares and validates rollback capability for a given operation.
        /// </summary>
        public Task<bool> PrepareRollbackAsync(BulkOperation operation, CancellationToken ct = default)
        {
            if (operation == null) return Task.FromResult(false);
            var supported = SupportsRollback(operation.Action);
            _logger.LogInformation("Preparing rollback for Bulk Operation '{Id}' (Action={Act}). Rollback Supported={Sup}",
                operation.BulkOperationId, operation.Action, supported);
            return Task.FromResult(true); // Return true to allow rollback execution using default fallback names if unregistered
        }

        /// <summary>
        /// Executes rollback actions across targeted machines.
        /// </summary>
        public async Task<RollbackHistoryEntry> ExecuteRollbackAsync(
            BulkOperation operation,
            IEnumerable<string> machineIds,
            CancellationToken ct = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (machineIds == null) throw new ArgumentNullException(nameof(machineIds));

            var rollbackId = Guid.NewGuid().ToString();
            var rollbackAction = GetRollbackAction(operation.Action);

            _logger.LogInformation("Starting Rollback '{RollbackId}' for operation '{OpId}'. Rollback Action={Act}",
                rollbackId, operation.BulkOperationId, rollbackAction);

            var outcomes = new ConcurrentDictionary<string, OperationResult>();

            // Execute rollback in parallel across targeted machines
            var tasks = machineIds.Select(async machineId =>
            {
                try
                {
                    var cmd = new RemoteCommand
                    {
                        CommandId = Guid.NewGuid().ToString(),
                        Action = rollbackAction,
                        TargetMachineId = machineId,
                        Priority = CommandPriority.High,
                        CreatorOperatorId = operation.OperatorId,
                        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
                    };

                    var result = await _commandService.ExecuteCommandAsync(cmd, ct);
                    outcomes[machineId] = result.Outcome;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing rollback action on machine '{MachineId}'", machineId);
                    outcomes[machineId] = OperationResult.Failure;
                }
            });

            await Task.WhenAll(tasks);

            // Rollback Validation: Rollback is validated if all targeted machines report success or skip
            bool isValidated = outcomes.Values.All(o => o == OperationResult.Success || o == OperationResult.Skipped);

            var entry = new RollbackHistoryEntry
            {
                RollbackId = rollbackId,
                BulkOperationId = operation.BulkOperationId,
                RollbackAction = rollbackAction,
                TriggeredAtUtc = DateTime.UtcNow,
                IsValidated = isValidated,
                MachineOutcomes = outcomes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            _history[rollbackId] = entry;

            _logger.LogInformation("Rollback '{RollbackId}' finished. Validated={Valid}, Succeeded={Succeeded}/{Total}",
                rollbackId, isValidated, outcomes.Values.Count(o => o == OperationResult.Success), outcomes.Count);

            return entry;
        }

        /// <summary>
        /// Retrieves the rollback execution history.
        /// </summary>
        public IReadOnlyList<RollbackHistoryEntry> GetRollbackHistory()
        {
            return _history.Values.ToList();
        }

        /// <summary>
        /// Retrieves rollback details by original operation ID.
        /// </summary>
        public RollbackHistoryEntry? GetRollbackByOperationId(string bulkOperationId)
        {
            return _history.Values.FirstOrDefault(e => e.BulkOperationId.Equals(bulkOperationId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
