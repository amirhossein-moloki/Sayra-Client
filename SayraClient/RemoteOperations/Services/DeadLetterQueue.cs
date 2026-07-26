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
    public class DeadLetterQueue : IDeadLetterQueue
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly IRemoteCommandRepository _repository;
        private readonly ILogger<DeadLetterQueue> _logger;

        public DeadLetterQueue(
            ILocalDatabaseService databaseService,
            IRemoteCommandRepository repository,
            ILogger<DeadLetterQueue> _loggerMock)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = _loggerMock ?? throw new ArgumentNullException(nameof(_loggerMock));
        }

        public async Task MoveToDeadLetterAsync(RemoteCommandHistory command, string failureReason, int retryCount, CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            _logger.LogWarning("Moving command {CommandId} ({Action}) permanently to Dead Letter Queue (DLQ). Reason: {Reason}",
                command.CommandId, command.Action, failureReason);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. Insert into DeadLetterCommand table
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO DeadLetterCommand (
                            CommandId, OriginalAction, FailureReason, RetryCount, CreatedAt, MovedAt
                        ) VALUES (
                            $id, $action, $reason, $retry, $created, $moved
                        );";

                    cmd.Parameters.Add(CreateParam(cmd, "$id", command.CommandId));
                    cmd.Parameters.Add(CreateParam(cmd, "$action", command.Action));
                    cmd.Parameters.Add(CreateParam(cmd, "$reason", failureReason));
                    cmd.Parameters.Add(CreateParam(cmd, "$retry", retryCount));
                    cmd.Parameters.Add(CreateParam(cmd, "$created", command.ReceivedAt));
                    cmd.Parameters.Add(CreateParam(cmd, "$moved", DateTime.UtcNow.ToString("O")));

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // 2. Update status in history to FAILED_DLQ or similar so it's queryable but never re-executed.
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        UPDATE RemoteCommandHistory
                        SET Status = 'FAILED_DLQ', ErrorMessage = $reason
                        WHERE CommandId = $id;";
                    cmd.Parameters.Add(CreateParam(cmd, "$reason", $"DLQ: {failureReason}"));
                    cmd.Parameters.Add(CreateParam(cmd, "$id", command.CommandId));
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to move command to dead letter queue.");
                throw;
            }
        }

        public async Task<List<DeadLetterCommand>> GetDeadLetterCommandsAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT CommandId, OriginalAction, FailureReason, RetryCount, CreatedAt, MovedAt
                FROM DeadLetterCommand
                ORDER BY MovedAt DESC;";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<DeadLetterCommand>();
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new DeadLetterCommand
                {
                    CommandId = reader.GetString(0),
                    OriginalAction = reader.GetString(1),
                    FailureReason = reader.GetString(2),
                    RetryCount = reader.GetInt32(3),
                    CreatedAt = reader.GetString(4),
                    MovedAt = reader.GetString(5)
                });
            }

            return list;
        }

        public async Task<DeadLetterCommand?> GetDeadLetterCommandAsync(string commandId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(commandId)) throw new ArgumentException("Command ID cannot be null or empty.", nameof(commandId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT CommandId, OriginalAction, FailureReason, RetryCount, CreatedAt, MovedAt
                FROM DeadLetterCommand
                WHERE CommandId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", commandId));

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new DeadLetterCommand
                {
                    CommandId = reader.GetString(0),
                    OriginalAction = reader.GetString(1),
                    FailureReason = reader.GetString(2),
                    RetryCount = reader.GetInt32(3),
                    CreatedAt = reader.GetString(4),
                    MovedAt = reader.GetString(5)
                };
            }

            return null;
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
