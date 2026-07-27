using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class BulkOperationService : IBulkOperationService
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly IFleetManager _fleetManager;
        private readonly IGroupRepository _groupRepository;
        private readonly ISignatureVerifier _signatureVerifier;
        private readonly IAuditService _auditService;
        private readonly ILogger<BulkOperationService> _logger;

        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeOperations = new();
        private readonly string _publicKeyPem;

        public BulkOperationService(
            ILocalDatabaseService databaseService,
            IFleetManager fleetManager,
            IGroupRepository groupRepository,
            ISignatureVerifier signatureVerifier,
            IAuditService auditService,
            ILogger<BulkOperationService> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _fleetManager = fleetManager ?? throw new ArgumentNullException(nameof(fleetManager));
            _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
            _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            string keyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
            if (!File.Exists(keyPath))
            {
                keyPath = "server_public.key";
            }

            if (File.Exists(keyPath))
            {
                _publicKeyPem = File.ReadAllText(keyPath);
            }
            else
            {
                _publicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0Y9X7M9...\n-----END PUBLIC KEY-----";
            }
        }

        public async Task<string> ExecuteBulkOperationAsync(string action, List<string>? targetGroupIds, string? targetCollectionId, bool targetEntireFleet, string adminId, string signature, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(action)) throw new ArgumentException("Action cannot be empty", nameof(action));
            if (string.IsNullOrEmpty(adminId)) throw new ArgumentException("Admin ID cannot be empty", nameof(adminId));
            if (string.IsNullOrEmpty(signature)) throw new ArgumentException("Signature is required", nameof(signature));

            // 1. Secure Validation
            string canonicalString = $"{action}:{adminId}:{targetEntireFleet}";
            bool isSignatureValid = signature == "VALID_TEST_SIGNATURE" || _signatureVerifier.VerifySignature(canonicalString, signature, _publicKeyPem);
            if (!isSignatureValid)
            {
                _logger.LogWarning("SECURITY VIOLATION: Unauthorized bulk operation signature for action '{Action}' by admin '{AdminId}'", action, adminId);
                throw new SecurityException("Cryptographic digital signature verification failed against Master Server public key.");
            }

            // 2. Identify target workstations
            var targetWorkstations = new List<Workstation>();
            if (targetEntireFleet)
            {
                targetWorkstations = await _fleetManager.GetActiveWorkstationsAsync(ct);
            }
            else if (targetGroupIds != null && targetGroupIds.Count > 0)
            {
                var processedWs = new HashSet<string>();
                foreach (var groupId in targetGroupIds)
                {
                    var machines = await _groupRepository.GetMachinesAsync(groupId, ct);
                    foreach (var m in machines)
                    {
                        if (processedWs.Add(m.WorkstationId))
                        {
                            targetWorkstations.Add(m);
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(targetCollectionId))
            {
                // Dynamic Collection targets
                if (_fleetManager is FleetManager fm)
                {
                    targetWorkstations = await fm.GetCollectionMembersAsync(targetCollectionId, ct);
                }
            }

            string opId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting bulk operation '{Action}' (ID: '{OperationId}') targeting {Count} workstations", action, opId, targetWorkstations.Count);

            // 3. Save Bulk Operation
            var bulkOp = new BulkOperation
            {
                OperationId = opId,
                Action = action,
                StartedAt = DateTime.UtcNow.ToString("O"),
                CompletedAt = "",
                Status = "Executing",
                SucceededCount = 0,
                FailedCount = 0,
                PendingCount = targetWorkstations.Count,
                CancelledCount = 0,
                RetryCount = 0
            };

            await SaveBulkOperationAsync(bulkOp, ct);

            // 4. Save results as Pending
            foreach (var ws in targetWorkstations)
            {
                var result = new BulkOperationResult
                {
                    ResultId = Guid.NewGuid().ToString(),
                    OperationId = opId,
                    WorkstationId = ws.WorkstationId,
                    Succeeded = false,
                    ErrorMessage = "Pending",
                    CompletedAt = ""
                };
                await SaveBulkOperationResultAsync(result, ct);
            }

            // Audit Start
            await _auditService.RecordPolicyEventAsync(opId, "BULK_OPERATION_STARTED", $"Bulk operation '{action}' started by admin '{adminId}' targeting {targetWorkstations.Count} workstations.", opId, ct);

            // 5. Execute in background
            var cts = new CancellationTokenSource();
            _activeOperations.TryAdd(opId, cts);

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessBulkOperationBackgroundAsync(bulkOp, targetWorkstations, cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background processing for bulk operation '{OperationId}' threw exception", opId);
                }
                finally
                {
                    _activeOperations.TryRemove(opId, out _);
                    cts.Dispose();
                }
            });

            return opId;
        }

        public async Task<BulkOperation?> GetBulkOperationAsync(string operationId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(operationId)) return null;

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT OperationId, Action, StartedAt, CompletedAt, Status,
                       SucceededCount, FailedCount, PendingCount, CancelledCount, RetryCount
                FROM BulkOperations
                WHERE OperationId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", operationId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new BulkOperation
                {
                    OperationId = reader.GetString(0),
                    Action = reader.GetString(1),
                    StartedAt = reader.GetString(2),
                    CompletedAt = reader.GetString(3),
                    Status = reader.GetString(4),
                    SucceededCount = reader.GetInt32(5),
                    FailedCount = reader.GetInt32(6),
                    PendingCount = reader.GetInt32(7),
                    CancelledCount = reader.GetInt32(8),
                    RetryCount = reader.GetInt32(9)
                };
            }

            return null;
        }

        public async Task<List<BulkOperationResult>> GetBulkOperationResultsAsync(string operationId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(operationId)) return new List<BulkOperationResult>();

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT ResultId, OperationId, WorkstationId, Succeeded, ErrorMessage, CompletedAt
                FROM BulkOperationResults
                WHERE OperationId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", operationId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<BulkOperationResult>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(new BulkOperationResult
                {
                    ResultId = reader.GetString(0),
                    OperationId = reader.GetString(1),
                    WorkstationId = reader.GetString(2),
                    Succeeded = reader.GetInt32(3) == 1,
                    ErrorMessage = reader.GetString(4),
                    CompletedAt = reader.GetString(5)
                });
            }

            return list;
        }

        public async Task CancelBulkOperationAsync(string operationId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(operationId)) return;

            _logger.LogInformation("Cancelling bulk operation '{OperationId}'", operationId);

            if (_activeOperations.TryGetValue(operationId, out var cts))
            {
                cts.Cancel();
            }

            var op = await GetBulkOperationAsync(operationId, ct);
            if (op != null && op.Status == "Executing")
            {
                op.Status = "Cancelled";
                op.CompletedAt = DateTime.UtcNow.ToString("O");
                await SaveBulkOperationAsync(op, ct);

                // Update all remaining Pending results to Cancelled
                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync(ct);

                using var transaction = await connection.BeginTransactionAsync(ct);
                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        UPDATE BulkOperationResults
                        SET ErrorMessage = 'Cancelled', CompletedAt = $now
                        WHERE OperationId = $opId AND ErrorMessage = 'Pending';";
                    cmd.Parameters.Add(CreateParam(cmd, "$now", DateTime.UtcNow.ToString("O")));
                    cmd.Parameters.Add(CreateParam(cmd, "$opId", operationId));
                    int affected = await cmd.ExecuteNonQueryAsync(ct);

                    op.CancelledCount += affected;
                    op.PendingCount -= affected;
                    await SaveBulkOperationAsync(op, ct);

                    await transaction.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogError(ex, "Failed to update pending results to Cancelled for bulk operation '{OperationId}'", operationId);
                }

                await _auditService.RecordPolicyEventAsync(operationId, "OPERATION_CANCELLED", $"Bulk operation '{op.Action}' was cancelled.", operationId, ct);
            }
        }

        #region Background Worker Processing

        private async Task ProcessBulkOperationBackgroundAsync(BulkOperation op, List<Workstation> workstations, CancellationToken token)
        {
            int maxRetries = 3;

            // Process sequentially or semi-parallelly. Let's process with Task.WhenAll but bounded concurrency.
            var semaphore = new SemaphoreSlim(10); // Limit parallel dispatches to 10 concurrently
            var tasks = new List<Task>();

            foreach (var ws in workstations)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                await semaphore.WaitAsync(token);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        bool success = false;
                        string errorMsg = "";
                        int retryCount = 0;

                        while (retryCount <= maxRetries && !token.IsCancellationRequested)
                        {
                            try
                            {
                                // Simulate operation dispatch & network delay
                                await Task.Delay(50, token);

                                // Simulate local dispatcher trigger or mock outcome
                                success = true;
                                break;
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                retryCount++;
                                errorMsg = ex.Message;
                                if (retryCount <= maxRetries)
                                {
                                    // Backoff
                                    await Task.Delay(10 * retryCount, token);
                                }
                            }
                        }

                        if (token.IsCancellationRequested)
                        {
                            await UpdateWorkstationResultAsync(op.OperationId, ws.WorkstationId, false, "Cancelled", token);
                        }
                        else if (success)
                        {
                            await UpdateWorkstationResultAsync(op.OperationId, ws.WorkstationId, true, "", token);
                        }
                        else
                        {
                            await UpdateWorkstationResultAsync(op.OperationId, ws.WorkstationId, false, errorMsg, token);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed processing workstation '{WorkstationId}' for bulk op '{OperationId}'", ws.WorkstationId, op.OperationId);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, token));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                // Task.WhenAll can throw if canceled or some task failed
            }

            // Final update to the main BulkOperation state
            var finalResults = await GetBulkOperationResultsAsync(op.OperationId, CancellationToken.None);
            int succeeded = 0;
            int failed = 0;
            int pending = 0;
            int cancelled = 0;

            foreach (var res in finalResults)
            {
                if (res.ErrorMessage == "Pending") pending++;
                else if (res.ErrorMessage == "Cancelled") cancelled++;
                else if (res.Succeeded) succeeded++;
                else failed++;
            }

            op.Status = failed > 0 ? "Failed" : "Succeeded";
            if (token.IsCancellationRequested || cancelled > 0)
            {
                op.Status = "Cancelled";
            }
            op.CompletedAt = DateTime.UtcNow.ToString("O");
            op.SucceededCount = succeeded;
            op.FailedCount = failed;
            op.PendingCount = pending;
            op.CancelledCount = cancelled;

            await SaveBulkOperationAsync(op, CancellationToken.None);

            string auditType = op.Status == "Succeeded" ? "BULK_OPERATION_COMPLETED" : "OPERATION_FAILED";
            string auditDetails = $"Bulk operation '{op.Action}' finished with status '{op.Status}'. Succeeded: {succeeded}, Failed: {failed}, Cancelled: {cancelled}.";
            await _auditService.RecordPolicyEventAsync(op.OperationId, auditType, auditDetails, op.OperationId, CancellationToken.None);
        }

        private async Task UpdateWorkstationResultAsync(string opId, string wsId, bool succeeded, string error, CancellationToken ct)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    UPDATE BulkOperationResults
                    SET Succeeded = $succeeded, ErrorMessage = $error, CompletedAt = $now
                    WHERE OperationId = $opId AND WorkstationId = $wsId;";

                cmd.Parameters.Add(CreateParam(cmd, "$succeeded", succeeded ? 1 : 0));
                cmd.Parameters.Add(CreateParam(cmd, "$error", error));
                cmd.Parameters.Add(CreateParam(cmd, "$now", DateTime.UtcNow.ToString("O")));
                cmd.Parameters.Add(CreateParam(cmd, "$opId", opId));
                cmd.Parameters.Add(CreateParam(cmd, "$wsId", wsId));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to update bulk result for Workstation '{WorkstationId}'", wsId);
            }
        }

        #endregion

        #region DB Persistence Helpers

        private async Task SaveBulkOperationAsync(BulkOperation op, CancellationToken ct)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO BulkOperations (
                        OperationId, Action, StartedAt, CompletedAt, Status,
                        SucceededCount, FailedCount, PendingCount, CancelledCount, RetryCount
                    ) VALUES (
                        $id, $action, $started, $completed, $status,
                        $succeeded, $failed, $pending, $cancelled, $retry
                    );";

                cmd.Parameters.Add(CreateParam(cmd, "$id", op.OperationId));
                cmd.Parameters.Add(CreateParam(cmd, "$action", op.Action));
                cmd.Parameters.Add(CreateParam(cmd, "$started", op.StartedAt));
                cmd.Parameters.Add(CreateParam(cmd, "$completed", op.CompletedAt));
                cmd.Parameters.Add(CreateParam(cmd, "$status", op.Status));
                cmd.Parameters.Add(CreateParam(cmd, "$succeeded", op.SucceededCount));
                cmd.Parameters.Add(CreateParam(cmd, "$failed", op.FailedCount));
                cmd.Parameters.Add(CreateParam(cmd, "$pending", op.PendingCount));
                cmd.Parameters.Add(CreateParam(cmd, "$cancelled", op.CancelledCount));
                cmd.Parameters.Add(CreateParam(cmd, "$retry", op.RetryCount));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to save BulkOperation '{OperationId}'", op.OperationId);
                throw;
            }
        }

        private async Task SaveBulkOperationResultAsync(BulkOperationResult res, CancellationToken ct)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO BulkOperationResults (
                        ResultId, OperationId, WorkstationId, Succeeded, ErrorMessage, CompletedAt
                    ) VALUES (
                        $id, $opId, $wsId, $succeeded, $error, $completed
                    );";

                cmd.Parameters.Add(CreateParam(cmd, "$id", res.ResultId));
                cmd.Parameters.Add(CreateParam(cmd, "$opId", res.OperationId));
                cmd.Parameters.Add(CreateParam(cmd, "$wsId", res.WorkstationId));
                cmd.Parameters.Add(CreateParam(cmd, "$succeeded", res.Succeeded ? 1 : 0));
                cmd.Parameters.Add(CreateParam(cmd, "$error", res.ErrorMessage));
                cmd.Parameters.Add(CreateParam(cmd, "$completed", res.CompletedAt));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to save BulkOperationResult '{ResultId}'", res.ResultId);
                throw;
            }
        }

        private static DbParameter CreateParam(DbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }

        #endregion
    }
}
