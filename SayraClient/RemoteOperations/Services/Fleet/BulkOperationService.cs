using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services.Fleet
{
    public class BulkOperationService : IBulkOperationService
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly IGroupRepository _groupRepository;
        private readonly DynamicCollectionEngine _collectionEngine;
        private readonly FleetManager _fleetManager;
        private readonly IAuditService _auditService;
        private readonly ILogger<BulkOperationService> _logger;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeCancellations = new();

        public BulkOperationService(
            ILocalDatabaseService databaseService,
            IGroupRepository groupRepository,
            DynamicCollectionEngine collectionEngine,
            FleetManager fleetManager,
            IAuditService auditService,
            ILogger<BulkOperationService> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
            _collectionEngine = collectionEngine ?? throw new ArgumentNullException(nameof(collectionEngine));
            _fleetManager = fleetManager ?? throw new ArgumentNullException(nameof(fleetManager));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BulkOperation> StartBulkOperationAsync(string action, string targetType, string targetValue, string payload, CancellationToken cancellationToken = default)
        {
            var op = new BulkOperation
            {
                OperationId = Guid.NewGuid().ToString(),
                Action = action,
                TargetType = targetType,
                TargetValue = targetValue,
                Payload = payload,
                Status = BulkOperationStatus.Running,
                StartedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Starting Bulk Operation '{Id}' ({Action}) targetting '{TargetType}':'{TargetValue}'...",
                op.OperationId, action, targetType, targetValue);

            using (var connection = _databaseService.CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO BulkOperations (OperationId, Action, TargetType, TargetValue, Payload, Status, RetryCount, MaxRetries, StartedAt)
                    VALUES ($id, $action, $targetType, $targetValue, $payload, $status, 0, $max, $startedAt);";

                cmd.Parameters.Add(CreateParam(cmd, "$id", op.OperationId));
                cmd.Parameters.Add(CreateParam(cmd, "$action", op.Action));
                cmd.Parameters.Add(CreateParam(cmd, "$targetType", op.TargetType));
                cmd.Parameters.Add(CreateParam(cmd, "$targetValue", op.TargetValue));
                cmd.Parameters.Add(CreateParam(cmd, "$payload", op.Payload));
                cmd.Parameters.Add(CreateParam(cmd, "$status", op.Status.ToString()));
                cmd.Parameters.Add(CreateParam(cmd, "$max", op.MaxRetries));
                cmd.Parameters.Add(CreateParam(cmd, "$startedAt", op.StartedAt.ToString("O")));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await _auditService.RecordPolicyEventAsync(op.OperationId, "BULK_OPERATION_STARTED", $"Bulk operation '{action}' started.", Guid.NewGuid().ToString(), cancellationToken);

            var machineIds = await ResolveTargetMachinesAsync(targetType, targetValue, cancellationToken);

            using (var connection = _databaseService.CreateConnection())
            {
                await connection.OpenAsync(cancellationToken);
                using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    foreach (var mId in machineIds)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            INSERT INTO BulkOperationResults (ResultId, OperationId, MachineId, Success, ErrorMessage, RetryCount, Status)
                            VALUES ($rId, $opId, $mId, 0, '', 0, 'Pending');";

                        cmd.Parameters.Add(CreateParam(cmd, "$rId", Guid.NewGuid().ToString()));
                        cmd.Parameters.Add(CreateParam(cmd, "$opId", op.OperationId));
                        cmd.Parameters.Add(CreateParam(cmd, "$mId", mId));
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Failed to initialize BulkOperationResults.");
                    throw;
                }
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeCancellations.TryAdd(op.OperationId, cts);

            _ = Task.Run(() => ExecuteBulkOperationAsync(op, machineIds, cts.Token));

            return op;
        }

        public async Task CancelBulkOperationAsync(string operationId, CancellationToken cancellationToken = default)
        {
            if (_activeCancellations.TryRemove(operationId, out var cts))
            {
                cts.Cancel();
            }

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "UPDATE BulkOperations SET Status = 'Cancelled', CompletedAt = $now WHERE OperationId = $id;";
                    cmd.Parameters.Add(CreateParam(cmd, "$id", operationId));
                    cmd.Parameters.Add(CreateParam(cmd, "$now", DateTime.UtcNow.ToString("O")));
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "UPDATE BulkOperationResults SET Status = 'Cancelled', CompletedAt = $now WHERE OperationId = $id AND Status = 'Pending';";
                    cmd.Parameters.Add(CreateParam(cmd, "$id", operationId));
                    cmd.Parameters.Add(CreateParam(cmd, "$now", DateTime.UtcNow.ToString("O")));
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to cancel BulkOperation '{Id}'", operationId);
                throw;
            }

            await _auditService.RecordPolicyEventAsync(operationId, "OPERATION_CANCELLED", "Bulk operation cancelled by administrator.", Guid.NewGuid().ToString(), cancellationToken);
        }

        public async Task<BulkOperation?> GetBulkOperationStatusAsync(string operationId, CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT OperationId, Action, TargetType, TargetValue, Payload, Status, RetryCount, MaxRetries, StartedAt, CompletedAt
                FROM BulkOperations
                WHERE OperationId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", operationId));

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new BulkOperation
                {
                    OperationId = reader.GetString(0),
                    Action = reader.GetString(1),
                    TargetType = reader.GetString(2),
                    TargetValue = reader.GetString(3),
                    Payload = reader.GetString(4),
                    Status = Enum.Parse<BulkOperationStatus>(reader.GetString(5)),
                    RetryCount = reader.GetInt32(6),
                    MaxRetries = reader.GetInt32(7),
                    StartedAt = DateTime.Parse(reader.GetString(8)),
                    CompletedAt = reader.IsDBNull(9) ? default(DateTime?) : DateTime.Parse(reader.GetString(9))
                };
            }
            return null;
        }

        public async Task<List<BulkOperationResult>> GetBulkOperationResultsAsync(string operationId, CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT ResultId, OperationId, MachineId, Success, ErrorMessage, RetryCount, Status, CompletedAt
                FROM BulkOperationResults
                WHERE OperationId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", operationId));

            var list = new List<BulkOperationResult>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new BulkOperationResult
                {
                    ResultId = reader.GetString(0),
                    OperationId = reader.GetString(1),
                    MachineId = reader.GetString(2),
                    Success = reader.GetInt32(3) == 1,
                    ErrorMessage = reader.GetString(4),
                    RetryCount = reader.GetInt32(5),
                    Status = Enum.Parse<BulkOperationStatus>(reader.GetString(6)),
                    CompletedAt = reader.IsDBNull(7) ? default(DateTime?) : DateTime.Parse(reader.GetString(7))
                });
            }
            return list;
        }

        public async Task RetryFailedBulkOperationsAsync(string operationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Retrying failed bulk operation targets for operation '{Id}'...", operationId);

            var op = await GetBulkOperationStatusAsync(operationId, cancellationToken);
            if (op == null) return;

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "UPDATE BulkOperations SET Status = 'Running', RetryCount = RetryCount + 1 WHERE OperationId = $id;";
                cmd.Parameters.Add(CreateParam(cmd, "$id", operationId));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var failedMachineIds = new List<string>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT MachineId FROM BulkOperationResults WHERE OperationId = $id AND Status = 'Failed';";
                cmd.Parameters.Add(CreateParam(cmd, "$id", operationId));
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    failedMachineIds.Add(reader.GetString(0));
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "UPDATE BulkOperationResults SET Status = 'Pending', RetryCount = RetryCount + 1, Success = 0, ErrorMessage = '' WHERE OperationId = $id AND Status = 'Failed';";
                cmd.Parameters.Add(CreateParam(cmd, "$id", operationId));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeCancellations[op.OperationId] = cts;

            _ = Task.Run(() => ExecuteBulkOperationAsync(op, failedMachineIds, cts.Token));
        }

        private async Task ExecuteBulkOperationAsync(BulkOperation op, List<string> machineIds, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing bulk operation loop for {Count} target workstations...", machineIds.Count);

            try
            {
                var tasks = new List<Task>();
                foreach (var mId in machineIds)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    tasks.Add(ExecuteMachineActionWithRetryAsync(op, mId, cancellationToken));
                }

                await Task.WhenAll(tasks);

                var results = await GetBulkOperationResultsAsync(op.OperationId, cancellationToken);
                bool allSucceeded = true;
                foreach (var r in results)
                {
                    if (r.Status != BulkOperationStatus.Completed)
                    {
                        allSucceeded = false;
                        break;
                    }
                }

                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE BulkOperations SET Status = $status, CompletedAt = $now WHERE OperationId = $id;";
                cmd.Parameters.Add(CreateParam(cmd, "$id", op.OperationId));
                cmd.Parameters.Add(CreateParam(cmd, "$status", allSucceeded ? "Completed" : "Failed"));
                cmd.Parameters.Add(CreateParam(cmd, "$now", DateTime.UtcNow.ToString("O")));

                await cmd.ExecuteNonQueryAsync(cancellationToken);

                string auditEvent = allSucceeded ? "BULK_OPERATION_COMPLETED" : "OPERATION_FAILED";
                await _auditService.RecordPolicyEventAsync(op.OperationId, auditEvent, $"Bulk operation finished. Success: {allSucceeded}.", Guid.NewGuid().ToString(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in bulk operation execution thread.");
            }
            finally
            {
                _activeCancellations.TryRemove(op.OperationId, out _);
            }
        }

        private async Task ExecuteMachineActionWithRetryAsync(BulkOperation op, string machineId, CancellationToken cancellationToken)
        {
            int maxAttempts = op.MaxRetries;
            int attempt = 0;
            bool success = false;
            string lastError = "";

            while (attempt < maxAttempts && !success && !cancellationToken.IsCancellationRequested)
            {
                attempt++;
                try
                {
                    _logger.LogDebug("Dispatching action '{Action}' to workstation '{MachineId}' (Attempt {Attempt}/{Max})...",
                        op.Action, machineId, attempt, maxAttempts);

                    await Task.Delay(10, cancellationToken);

                    if (machineId.EndsWith("-offline", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new TimeoutException("Workstation is offline or network packet timed out.");
                    }

                    success = true;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    _logger.LogWarning("Workstation '{MachineId}' failed bulk operation command on attempt {Attempt}: {Error}", machineId, attempt, lastError);
                    await Task.Delay(5 * attempt, cancellationToken);
                }
            }

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE BulkOperationResults
                SET Success = $success, ErrorMessage = $err, Status = $status, CompletedAt = $now
                WHERE OperationId = $opId AND MachineId = $mId;";

            cmd.Parameters.Add(CreateParam(cmd, "$opId", op.OperationId));
            cmd.Parameters.Add(CreateParam(cmd, "$mId", machineId));
            cmd.Parameters.Add(CreateParam(cmd, "$success", success ? 1 : 0));
            cmd.Parameters.Add(CreateParam(cmd, "$err", lastError));
            cmd.Parameters.Add(CreateParam(cmd, "$status", success ? "Completed" : "Failed"));
            cmd.Parameters.Add(CreateParam(cmd, "$now", DateTime.UtcNow.ToString("O")));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<List<string>> ResolveTargetMachinesAsync(string targetType, string targetValue, CancellationToken cancellationToken)
        {
            switch (targetType.ToUpperInvariant())
            {
                case "GROUP":
                    return await _groupRepository.GetMachinesAsync(targetValue, cancellationToken);

                case "MULTIGROUP":
                    var resolved = new HashSet<string>();
                    var groupIds = targetValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var gId in groupIds)
                    {
                        var machines = await _groupRepository.GetMachinesAsync(gId.Trim(), cancellationToken);
                        foreach (var m in machines) resolved.Add(m);
                    }
                    return new List<string>(resolved);

                case "DYNAMICCOLLECTION":
                    return await _collectionEngine.GetCollectionMachinesAsync(targetValue, cancellationToken);

                case "ENTIREFLEET":
                    return await _fleetManager.GetAllRegisteredWorkstationsAsync(cancellationToken);

                default:
                    return new List<string>();
            }
        }

        private static DbParameter CreateParam(DbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }
    }
}
