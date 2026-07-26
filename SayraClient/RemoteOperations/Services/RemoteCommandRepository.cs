using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class RemoteCommandRepository : IRemoteCommandRepository
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly ILogger<RemoteCommandRepository> _logger;

        public RemoteCommandRepository(
            ILocalDatabaseService databaseService,
            ILogger<RemoteCommandRepository> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SaveCommandAsync(RemoteCommandHistory command, CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            _logger.LogDebug("Saving command history for command ID {CommandId}.", command.CommandId);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO RemoteCommandHistory (
                        CommandId, Action, TargetPcId, SenderAdminId, PayloadJson,
                        Status, ErrorMessage, ReceivedAt, StartedAt, CompletedAt,
                        ExecutionDurationMs, Signature, RetryCount
                    ) VALUES (
                        $id, $action, $target, $sender, $payload,
                        $status, $error, $received, $started, $completed,
                        $duration, $signature, $retry
                    );";

                cmd.Parameters.Add(CreateParam(cmd, "$id", command.CommandId));
                cmd.Parameters.Add(CreateParam(cmd, "$action", command.Action));
                cmd.Parameters.Add(CreateParam(cmd, "$target", command.TargetPcId));
                cmd.Parameters.Add(CreateParam(cmd, "$sender", command.SenderAdminId));
                cmd.Parameters.Add(CreateParam(cmd, "$payload", (object?)command.PayloadJson ?? DBNull.Value));
                cmd.Parameters.Add(CreateParam(cmd, "$status", command.Status));
                cmd.Parameters.Add(CreateParam(cmd, "$error", (object?)command.ErrorMessage ?? DBNull.Value));
                cmd.Parameters.Add(CreateParam(cmd, "$received", command.ReceivedAt));
                cmd.Parameters.Add(CreateParam(cmd, "$started", (object?)command.StartedAt ?? DBNull.Value));
                cmd.Parameters.Add(CreateParam(cmd, "$completed", (object?)command.CompletedAt ?? DBNull.Value));
                cmd.Parameters.Add(CreateParam(cmd, "$duration", (object?)command.ExecutionDurationMs ?? DBNull.Value));
                cmd.Parameters.Add(CreateParam(cmd, "$signature", command.Signature));
                cmd.Parameters.Add(CreateParam(cmd, "$retry", command.RetryCount));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to save remote command history.");
                throw;
            }
        }

        public async Task<RemoteCommandHistory?> GetCommandAsync(string commandId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(commandId)) throw new ArgumentException("Command ID cannot be null or empty.", nameof(commandId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT CommandId, Action, TargetPcId, SenderAdminId, PayloadJson,
                       Status, ErrorMessage, ReceivedAt, StartedAt, CompletedAt,
                       ExecutionDurationMs, Signature, RetryCount
                FROM RemoteCommandHistory
                WHERE CommandId = $id;";

            cmd.Parameters.Add(CreateParam(cmd, "$id", commandId));

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapHistory(reader);
            }

            return null;
        }

        public async Task<List<RemoteCommandHistory>> GetPendingCommandsAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT CommandId, Action, TargetPcId, SenderAdminId, PayloadJson,
                       Status, ErrorMessage, ReceivedAt, StartedAt, CompletedAt,
                       ExecutionDurationMs, Signature, RetryCount
                FROM RemoteCommandHistory
                WHERE Status = 'PENDING'
                ORDER BY ReceivedAt ASC;";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<RemoteCommandHistory>();
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapHistory(reader));
            }

            return list;
        }

        public async Task UpdateStatusAsync(string commandId, string status, string? errorMessage = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(commandId)) throw new ArgumentException("Command ID cannot be null or empty.", nameof(commandId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                string? startedAtStr = null;
                using (var selectCmd = connection.CreateCommand())
                {
                    selectCmd.Transaction = transaction;
                    selectCmd.CommandText = "SELECT StartedAt FROM RemoteCommandHistory WHERE CommandId = $id;";
                    selectCmd.Parameters.Add(CreateParam(selectCmd, "$id", commandId));
                    using (var reader = await selectCmd.ExecuteReaderAsync(cancellationToken))
                    {
                        if (await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0))
                        {
                            startedAtStr = reader.GetString(0);
                        }
                    }
                }

                string? newStartedAtStr = startedAtStr;
                string? completedAtStr = null;
                long? durationMs = null;

                if (string.Equals(status, "EXECUTING", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(newStartedAtStr))
                    {
                        newStartedAtStr = DateTime.UtcNow.ToString("O");
                    }
                }
                else if (string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase))
                {
                    completedAtStr = DateTime.UtcNow.ToString("O");
                    if (!string.IsNullOrEmpty(startedAtStr) && DateTime.TryParse(startedAtStr, out var startedAt))
                    {
                        var completedAt = DateTime.Parse(completedAtStr);
                        durationMs = (long)(completedAt - startedAt).TotalMilliseconds;
                    }
                }

                using (var updateCmd = connection.CreateCommand())
                {
                    updateCmd.Transaction = transaction;
                    updateCmd.CommandText = @"
                        UPDATE RemoteCommandHistory
                        SET Status = $status,
                            ErrorMessage = COALESCE($error, ErrorMessage),
                            StartedAt = COALESCE($started, StartedAt),
                            CompletedAt = COALESCE($completed, CompletedAt),
                            ExecutionDurationMs = COALESCE($duration, ExecutionDurationMs)
                        WHERE CommandId = $id;";

                    updateCmd.Parameters.Add(CreateParam(updateCmd, "$status", status));
                    updateCmd.Parameters.Add(CreateParam(updateCmd, "$error", (object?)errorMessage ?? DBNull.Value));
                    updateCmd.Parameters.Add(CreateParam(updateCmd, "$started", (object?)newStartedAtStr ?? DBNull.Value));
                    updateCmd.Parameters.Add(CreateParam(updateCmd, "$completed", (object?)completedAtStr ?? DBNull.Value));
                    updateCmd.Parameters.Add(CreateParam(updateCmd, "$duration", (object?)durationMs ?? DBNull.Value));
                    updateCmd.Parameters.Add(CreateParam(updateCmd, "$id", commandId));

                    await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to update command status in history database.");
                throw;
            }
        }

        public async Task DeleteCommandAsync(string commandId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(commandId)) throw new ArgumentException("Command ID cannot be null or empty.", nameof(commandId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM RemoteCommandHistory WHERE CommandId = $id;";
                cmd.Parameters.Add(CreateParam(cmd, "$id", commandId));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to delete remote command.");
                throw;
            }
        }

        public async Task<List<RemoteCommandHistory>> GetHistoryAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT CommandId, Action, TargetPcId, SenderAdminId, PayloadJson,
                       Status, ErrorMessage, ReceivedAt, StartedAt, CompletedAt,
                       ExecutionDurationMs, Signature, RetryCount
                FROM RemoteCommandHistory
                ORDER BY ReceivedAt DESC;";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<RemoteCommandHistory>();
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapHistory(reader));
            }

            return list;
        }

        private static DbParameter CreateParam(DbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }

        private static RemoteCommandHistory MapHistory(DbDataReader reader)
        {
            return new RemoteCommandHistory
            {
                CommandId = reader.GetString(0),
                Action = reader.GetString(1),
                TargetPcId = reader.GetString(2),
                SenderAdminId = reader.GetString(3),
                PayloadJson = reader.IsDBNull(4) ? null : reader.GetString(4),
                Status = reader.GetString(5),
                ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                ReceivedAt = reader.GetString(7),
                StartedAt = reader.IsDBNull(8) ? null : reader.GetString(8),
                CompletedAt = reader.IsDBNull(9) ? null : reader.GetString(9),
                ExecutionDurationMs = reader.IsDBNull(10) ? null : reader.GetInt64(10),
                Signature = reader.GetString(11),
                RetryCount = reader.GetInt32(12)
            };
        }
    }
}
