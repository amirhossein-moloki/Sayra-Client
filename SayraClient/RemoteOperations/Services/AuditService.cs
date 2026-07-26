using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class AuditService : IAuditService
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly ILogger<AuditService> _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

        public AuditService(
            ILocalDatabaseService databaseService,
            ILogger<AuditService> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task RecordCommandReceivedAsync(string commandId, string action, string correlationId, CancellationToken cancellationToken = default)
        {
            return SaveAuditEntryAsync(commandId, "COMMAND_RECEIVED", $"Command '{action}' received.", correlationId, cancellationToken);
        }

        public Task RecordSecurityValidationResultAsync(string commandId, bool success, string reason, string correlationId, CancellationToken cancellationToken = default)
        {
            string eventType = success ? "SECURITY_VALIDATION_PASSED" : "SECURITY_VALIDATION_FAILED";
            return SaveAuditEntryAsync(commandId, eventType, reason, correlationId, cancellationToken);
        }

        public Task RecordExecutionStartedAsync(string commandId, string action, string correlationId, CancellationToken cancellationToken = default)
        {
            return SaveAuditEntryAsync(commandId, "EXECUTION_STARTED", $"Execution of command '{action}' started.", correlationId, cancellationToken);
        }

        public Task RecordExecutionCompletedAsync(string commandId, string action, string correlationId, CancellationToken cancellationToken = default)
        {
            return SaveAuditEntryAsync(commandId, "EXECUTION_COMPLETED", $"Execution of command '{action}' completed successfully.", correlationId, cancellationToken);
        }

        public Task RecordExecutionFailedAsync(string commandId, string action, string error, string correlationId, CancellationToken cancellationToken = default)
        {
            return SaveAuditEntryAsync(commandId, "EXECUTION_FAILED", $"Execution of command '{action}' failed. Error: {error}", correlationId, cancellationToken);
        }

        public async Task<List<AuditEntry>> GetAuditTrailAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT AuditId, CorrelationId, EventType, CommandId, Timestamp, Details, PreviousHash, CurrentHash
                FROM AuditEntry
                ORDER BY Timestamp ASC, AuditId ASC;";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<AuditEntry>();
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new AuditEntry
                {
                    AuditId = reader.GetString(0),
                    CorrelationId = reader.GetString(1),
                    EventType = reader.GetString(2),
                    CommandId = reader.GetString(3),
                    Timestamp = reader.GetString(4),
                    Details = reader.GetString(5),
                    PreviousHash = reader.GetString(6),
                    CurrentHash = reader.GetString(7)
                });
            }

            return list;
        }

        public async Task<bool> VerifyAuditChainIntegrityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting audit chain cryptographic integrity verification...");

            var trail = await GetAuditTrailAsync(cancellationToken);
            string expectedPreviousHash = GenesisHash;

            foreach (var entry in trail)
            {
                // Verify the PreviousHash field
                if (entry.PreviousHash != expectedPreviousHash)
                {
                    _logger.LogCritical("AUDIT TAMPER DETECTED: Chain broken at AuditID {AuditId}. Expected previous hash '{Expected}' but got '{Actual}'.",
                        entry.AuditId, expectedPreviousHash, entry.PreviousHash);
                    return false;
                }

                // Recalculate CurrentHash
                string calculatedHash = ComputeSha256Hash(entry, entry.PreviousHash);
                if (entry.CurrentHash != calculatedHash)
                {
                    _logger.LogCritical("AUDIT TAMPER DETECTED: Hash mismatch at AuditID {AuditId}. Calculated '{Calculated}' but stored was '{Stored}'.",
                        entry.AuditId, calculatedHash, entry.CurrentHash);
                    return false;
                }

                expectedPreviousHash = entry.CurrentHash;
            }

            _logger.LogInformation("Audit chain verification completed. Integrity verified successfully for {Count} entries.", trail.Count);
            return true;
        }

        private async Task SaveAuditEntryAsync(string commandId, string eventType, string details, string correlationId, CancellationToken cancellationToken)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync(cancellationToken);

                string previousHash = GenesisHash;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT CurrentHash FROM AuditEntry ORDER BY Timestamp DESC, AuditId DESC LIMIT 1;";
                    var result = await cmd.ExecuteScalarAsync(cancellationToken);
                    if (result != null && result != DBNull.Value)
                    {
                        previousHash = result.ToString()!;
                    }
                }

                var entry = new AuditEntry
                {
                    AuditId = Guid.NewGuid().ToString(),
                    CorrelationId = correlationId,
                    EventType = eventType,
                    CommandId = commandId,
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    Details = details,
                    PreviousHash = previousHash
                };

                entry.CurrentHash = ComputeSha256Hash(entry, previousHash);

                using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO AuditEntry (
                            AuditId, CorrelationId, EventType, CommandId, Timestamp, Details, PreviousHash, CurrentHash
                        ) VALUES (
                            $auditId, $correlationId, $eventType, $commandId, $timestamp, $details, $previousHash, $currentHash
                        );";

                    cmd.Parameters.Add(CreateParam(cmd, "$auditId", entry.AuditId));
                    cmd.Parameters.Add(CreateParam(cmd, "$correlationId", entry.CorrelationId));
                    cmd.Parameters.Add(CreateParam(cmd, "$eventType", entry.EventType));
                    cmd.Parameters.Add(CreateParam(cmd, "$commandId", entry.CommandId));
                    cmd.Parameters.Add(CreateParam(cmd, "$timestamp", entry.Timestamp));
                    cmd.Parameters.Add(CreateParam(cmd, "$details", entry.Details));
                    cmd.Parameters.Add(CreateParam(cmd, "$previousHash", entry.PreviousHash));
                    cmd.Parameters.Add(CreateParam(cmd, "$currentHash", entry.CurrentHash));

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Failed to insert audit entry into local database.");
                    throw;
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private static string ComputeSha256Hash(AuditEntry entry, string previousHash)
        {
            using var sha256 = SHA256.Create();
            string canonical = $"{entry.AuditId}:{entry.CorrelationId}:{entry.EventType}:{entry.CommandId}:{entry.Timestamp}:{entry.Details}:{previousHash}";
            byte[] bytes = Encoding.UTF8.GetBytes(canonical);
            byte[] hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hashBytes);
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
