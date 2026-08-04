using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.RemoteCommands.History
{
    /// <summary>
    /// Represents a persistent historical record of a remote command execution.
    /// </summary>
    public sealed record RemoteCommandHistoryEntry
    {
        /// <summary>Gets the unique command tracker identifier.</summary>
        public string CommandId { get; init; } = string.Empty;
        /// <summary>Gets the action descriptor verb executed.</summary>
        public string Action { get; init; } = string.Empty;
        /// <summary>Gets the targeted client machine identifier.</summary>
        public string TargetMachineId { get; init; } = string.Empty;
        /// <summary>Gets the execution status.</summary>
        public CommandStatus Status { get; init; } = CommandStatus.Pending;
        /// <summary>Gets the final operational result type.</summary>
        public OperationResult Outcome { get; init; } = OperationResult.ValidationError;
        /// <summary>Gets output logs, details or error messages.</summary>
        public string OutputMessage { get; init; } = string.Empty;
        /// <summary>Gets execution duration in milliseconds.</summary>
        public long ExecutionDurationMs { get; init; }
        /// <summary>Gets the number of retry attempts executed.</summary>
        public int RetryCount { get; init; }
        /// <summary>Gets the operator identifier of the executing administrator.</summary>
        public string CreatorOperatorId { get; init; } = string.Empty;
        /// <summary>Gets tracking correlation context.</summary>
        public string CorrelationId { get; init; } = string.Empty;
        /// <summary>Gets creation timestamp.</summary>
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
        /// <summary>Gets completion timestamp.</summary>
        public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Repository interface for saving, updating, and querying remote command history.
    /// </summary>
    public interface IRemoteCommandHistoryRepository
    {
        /// <summary>Saves or updates a command history record.</summary>
        Task SaveAsync(RemoteCommandHistoryEntry entry, CancellationToken ct = default);

        /// <summary>Retrieves a specific command execution record by identifier.</summary>
        Task<RemoteCommandHistoryEntry?> GetAsync(string commandId, CancellationToken ct = default);

        /// <summary>Retrieves all historical command records.</summary>
        Task<IReadOnlyList<RemoteCommandHistoryEntry>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Retrieves historical commands targeted at a specific machine.</summary>
        Task<IReadOnlyList<RemoteCommandHistoryEntry>> GetByMachineAsync(string machineId, CancellationToken ct = default);

        /// <summary>Purges historical logs older than a specified duration.</summary>
        Task PurgeOlderThanAsync(TimeSpan age, CancellationToken ct = default);
    }

    /// <summary>
    /// SQLCipher SQLite-based persistent repository for Remote Command History.
    /// Thread-safe and designed for high concurrent throughput.
    /// </summary>
    public sealed class RemoteCommandHistoryRepository : IRemoteCommandHistoryRepository
    {
        private readonly IFleetDatabaseContext _dbContext;
        private readonly ILogger<RemoteCommandHistoryRepository> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isInitialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteCommandHistoryRepository"/> class.
        /// </summary>
        public RemoteCommandHistoryRepository(IFleetDatabaseContext dbContext, ILogger<RemoteCommandHistoryRepository> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_isInitialized) return;

            await _lock.WaitAsync(ct);
            try
            {
                if (_isInitialized) return;

                _logger.LogInformation("Initializing local secure command history table...");
                using var conn = _dbContext.CreateConnection();
                await conn.OpenAsync(ct);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS RemoteCommandHistory (
                        CommandId TEXT PRIMARY KEY NOT NULL,
                        Action TEXT NOT NULL,
                        TargetMachineId TEXT NOT NULL,
                        Status TEXT NOT NULL,
                        Outcome TEXT NOT NULL,
                        OutputMessage TEXT,
                        ExecutionDurationMs INTEGER NOT NULL,
                        RetryCount INTEGER NOT NULL,
                        CreatorOperatorId TEXT NOT NULL,
                        CorrelationId TEXT NOT NULL,
                        CreatedAtUtc TEXT NOT NULL,
                        CompletedAtUtc TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS IDX_RemoteCommandHistory_Machine ON RemoteCommandHistory (TargetMachineId);
                    CREATE INDEX IF NOT EXISTS IDX_RemoteCommandHistory_Created ON RemoteCommandHistory (CreatedAtUtc);";
                await cmd.ExecuteNonQueryAsync(ct);

                _isInitialized = true;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task SaveAsync(RemoteCommandHistoryEntry entry, CancellationToken ct = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                using var conn = _dbContext.CreateConnection();
                await conn.OpenAsync(ct);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO RemoteCommandHistory (
                        CommandId, Action, TargetMachineId, Status, Outcome, OutputMessage, ExecutionDurationMs, RetryCount, CreatorOperatorId, CorrelationId, CreatedAtUtc, CompletedAtUtc
                    ) VALUES ($id, $act, $target, $status, $outcome, $msg, $duration, $retry, $creator, $corr, $created, $completed)
                    ON CONFLICT(CommandId) DO UPDATE SET
                        Status = excluded.Status,
                        Outcome = excluded.Outcome,
                        OutputMessage = excluded.OutputMessage,
                        ExecutionDurationMs = excluded.ExecutionDurationMs,
                        RetryCount = excluded.RetryCount,
                        CompletedAtUtc = excluded.CompletedAtUtc;";

                AddParameter(cmd, "$id", entry.CommandId);
                AddParameter(cmd, "$act", entry.Action);
                AddParameter(cmd, "$target", entry.TargetMachineId);
                AddParameter(cmd, "$status", entry.Status.ToString());
                AddParameter(cmd, "$outcome", entry.Outcome.ToString());
                AddParameter(cmd, "$msg", entry.OutputMessage);
                AddParameter(cmd, "$duration", entry.ExecutionDurationMs);
                AddParameter(cmd, "$retry", entry.RetryCount);
                AddParameter(cmd, "$creator", entry.CreatorOperatorId);
                AddParameter(cmd, "$corr", entry.CorrelationId);
                AddParameter(cmd, "$created", entry.CreatedAtUtc.ToString("O"));
                AddParameter(cmd, "$completed", entry.CompletedAtUtc.ToString("O"));

                await cmd.ExecuteNonQueryAsync(ct);
                _logger.LogInformation("Saved remote command history entry for CommandId {CommandId}.", entry.CommandId);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<RemoteCommandHistoryEntry?> GetAsync(string commandId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(commandId)) return null;
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                using var conn = _dbContext.CreateConnection();
                await conn.OpenAsync(ct);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM RemoteCommandHistory WHERE CommandId = $id;";
                AddParameter(cmd, "$id", commandId);

                using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    return MapFromReader(reader);
                }
                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<RemoteCommandHistoryEntry>> GetAllAsync(CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                using var conn = _dbContext.CreateConnection();
                await conn.OpenAsync(ct);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM RemoteCommandHistory ORDER BY CreatedAtUtc DESC;";

                var entries = new List<RemoteCommandHistoryEntry>();
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    entries.Add(MapFromReader(reader));
                }
                return entries;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<RemoteCommandHistoryEntry>> GetByMachineAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return Array.Empty<RemoteCommandHistoryEntry>();
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                using var conn = _dbContext.CreateConnection();
                await conn.OpenAsync(ct);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM RemoteCommandHistory WHERE TargetMachineId = $target ORDER BY CreatedAtUtc DESC;";
                AddParameter(cmd, "$target", machineId);

                var entries = new List<RemoteCommandHistoryEntry>();
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    entries.Add(MapFromReader(reader));
                }
                return entries;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task PurgeOlderThanAsync(TimeSpan age, CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                var threshold = DateTime.UtcNow.Subtract(age);

                using var conn = _dbContext.CreateConnection();
                await conn.OpenAsync(ct);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM RemoteCommandHistory WHERE CreatedAtUtc < $threshold;";
                AddParameter(cmd, "$threshold", threshold.ToString("O"));

                int rows = await cmd.ExecuteNonQueryAsync(ct);
                _logger.LogInformation("Purged {Count} historical command log rows older than {Threshold}.", rows, threshold);
            }
            finally
            {
                _lock.Release();
            }
        }

        private static void AddParameter(DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static RemoteCommandHistoryEntry MapFromReader(DbDataReader reader)
        {
            return new RemoteCommandHistoryEntry
            {
                CommandId = reader.GetString(reader.GetOrdinal("CommandId")),
                Action = reader.GetString(reader.GetOrdinal("Action")),
                TargetMachineId = reader.GetString(reader.GetOrdinal("TargetMachineId")),
                Status = Enum.Parse<CommandStatus>(reader.GetString(reader.GetOrdinal("Status"))),
                Outcome = Enum.Parse<OperationResult>(reader.GetString(reader.GetOrdinal("Outcome"))),
                OutputMessage = reader.IsDBNull(reader.GetOrdinal("OutputMessage")) ? string.Empty : reader.GetString(reader.GetOrdinal("OutputMessage")),
                ExecutionDurationMs = reader.GetInt64(reader.GetOrdinal("ExecutionDurationMs")),
                RetryCount = reader.GetInt32(reader.GetOrdinal("RetryCount")),
                CreatorOperatorId = reader.GetString(reader.GetOrdinal("CreatorOperatorId")),
                CorrelationId = reader.GetString(reader.GetOrdinal("CorrelationId")),
                CreatedAtUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAtUtc"))),
                CompletedAtUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletedAtUtc")))
            };
        }
    }
}
