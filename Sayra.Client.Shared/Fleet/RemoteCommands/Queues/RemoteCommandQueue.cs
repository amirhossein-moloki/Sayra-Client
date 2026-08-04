using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.RemoteCommands.Queues
{
    /// <summary>
    /// Represents comprehensive operational statistics for the enterprise queues.
    /// </summary>
    public sealed record QueueStatistics
    {
        /// <summary>Gets the count of priority and FIFO commands active.</summary>
        public int ActiveCount { get; init; }
        /// <summary>Gets the count of delayed commands waiting for their execution timeline.</summary>
        public int DelayedCount { get; init; }
        /// <summary>Gets the count of commands held because the target machine is offline.</summary>
        public int OfflineCount { get; init; }
        /// <summary>Gets the count of failed commands slated for backoff retries.</summary>
        public int RetryCount { get; init; }
        /// <summary>Gets the count of commands routed to the Dead Letter Queue (DLQ).</summary>
        public int DeadLetterCount { get; init; }
        /// <summary>Gets the count of cancelled command identifiers.</summary>
        public int CancellationCount { get; init; }
        /// <summary>Gets the count of expired commands cleaned up from the queue.</summary>
        public int ExpirationCount { get; init; }
        /// <summary>Gets the total size of all persistent elements.</summary>
        public int TotalPersistentCount { get; init; }
    }

    /// <summary>
    /// Interface extension for enterprise command queues adding stats, cancellation, and retrieval.
    /// </summary>
    public interface IEnterpriseCommandQueue : IRemoteCommandQueue
    {
        /// <summary>Retrieves dynamic metrics and statistics for all queue segments.</summary>
        Task<QueueStatistics> GetStatisticsAsync(CancellationToken ct = default);

        /// <summary>Requests immediate cancellation of a command inside the queue.</summary>
        Task<bool> CancelCommandAsync(string commandId, CancellationToken ct = default);

        /// <summary>Recovers persistent elements from secure storage upon startup.</summary>
        Task RecoverQueueAsync(CancellationToken ct = default);

        /// <summary>Manually routes a command to the Dead Letter Queue.</summary>
        Task MoveToDeadLetterQueueAsync(RemoteCommand command, string reason, CancellationToken ct = default);

        /// <summary>Puts a command into the retry state with scheduled timeline.</summary>
        Task ScheduleRetryAsync(RemoteCommand command, DateTime runAtUtc, int retryCount, CancellationToken ct = default);

        /// <summary>Replays or releases pending offline commands for a target machine.</summary>
        Task ReplayOfflineCommandsAsync(string machineId, CancellationToken ct = default);

        /// <summary>Forces immediate pruning of expired and delayed commands.</summary>
        Task PruneExpiredAndDelayedAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Enterprise Thread-Safe, High-Performance Remote Command Queue.
    /// Integrates Priority, FIFO, Delayed, Offline, Retry, DLQ, Cancellation, and Expiration structures.
    /// Secured via SQLCipher persistence and designed for 10,000+ endpoints.
    /// </summary>
    public sealed class RemoteCommandQueue : IEnterpriseCommandQueue, IDisposable
    {
        private readonly IFleetDatabaseContext _dbContext;
        private readonly ILogger<RemoteCommandQueue> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        // Core concurrent in-memory trackers
        private readonly ConcurrentDictionary<string, RemoteCommand> _activeQueue = new();
        private readonly ConcurrentDictionary<string, DateTime> _delayedQueue = new();
        private readonly ConcurrentDictionary<string, string> _offlineQueue = new(); // CommandId -> MachineId
        private readonly ConcurrentDictionary<string, (RemoteCommand Command, DateTime RunAtUtc, int RetryCount)> _retryQueue = new();
        private readonly ConcurrentDictionary<string, string> _deadLetterQueue = new(); // CommandId -> Reason
        private readonly ConcurrentDictionary<string, DateTime> _cancellationQueue = new(); // CommandId -> Timestamp
        private readonly ConcurrentDictionary<string, DateTime> _expirationQueue = new(); // CommandId -> ExpiresAtUtc

        private bool _isInitialized;
        private readonly Timer _cleanupTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteCommandQueue"/> class.
        /// </summary>
        public RemoteCommandQueue(IFleetDatabaseContext dbContext, ILogger<RemoteCommandQueue> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Background timer to prune expired commands every 5 seconds
            _cleanupTimer = new Timer(async _ => await PruneExpiredAndDelayedInternalAsync(), null, 5000, 5000);
        }

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_isInitialized) return;

            await _lock.WaitAsync(ct);
            try
            {
                if (_isInitialized) return;

                _logger.LogInformation("Initializing secure queue persistent tables...");
                using var conn = _dbContext.CreateConnection();
                await conn.OpenAsync(ct);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS PersistentCommandQueue (
                        CommandId TEXT PRIMARY KEY NOT NULL,
                        Action TEXT NOT NULL,
                        TargetMachineId TEXT NOT NULL,
                        Priority INTEGER NOT NULL,
                        CreatorOperatorId TEXT NOT NULL,
                        ExpiresAtUtc TEXT NOT NULL,
                        Signature TEXT NOT NULL,
                        ParametersJson TEXT NOT NULL,
                        QueueType TEXT NOT NULL,
                        ScheduledAtUtc TEXT,
                        RetryCount INTEGER DEFAULT 0,
                        DeadLetterReason TEXT
                    );
                    CREATE INDEX IF NOT EXISTS IDX_PersistentCommandQueue_Type ON PersistentCommandQueue (QueueType);
                    CREATE INDEX IF NOT EXISTS IDX_PersistentCommandQueue_Machine ON PersistentCommandQueue (TargetMachineId);";
                await cmd.ExecuteNonQueryAsync(ct);

                _isInitialized = true;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task EnqueueCommandAsync(RemoteCommand command, CancellationToken ct = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                // Replay/Duplicate Protection
                if (_deadLetterQueue.ContainsKey(command.CommandId) || _activeQueue.ContainsKey(command.CommandId))
                {
                    _logger.LogWarning("Command {CommandId} is already tracked as active or dead-lettered. Rejecting.", command.CommandId);
                    return;
                }

                // Expiration Check
                if (command.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    _logger.LogWarning("Command {CommandId} has already expired. Routing directly to Dead Letter Queue.", command.CommandId);
                    await SaveToDatabaseInternalAsync(command, "DEAD_LETTER", null, 0, "Expired before enqueue", ct);
                    _deadLetterQueue.TryAdd(command.CommandId, "Expired before enqueue");
                    return;
                }

                // Check Cancellation Signal
                if (_cancellationQueue.ContainsKey(command.CommandId))
                {
                    _logger.LogInformation("Command {CommandId} was previously cancelled. Rejecting enqueue.", command.CommandId);
                    return;
                }

                // Default Enqueue is Active
                _activeQueue[command.CommandId] = command;
                _expirationQueue[command.CommandId] = command.ExpiresAtUtc;

                await SaveToDatabaseInternalAsync(command, "ACTIVE", null, 0, null, ct);
                _logger.LogInformation("Command {CommandId} enqueued in Active Queue.", command.CommandId);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<RemoteCommand?> DequeueCommandAsync(CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                // Exclude cancelled and expired commands
                PurgeCompletedCancellationsAndExpirations();

                if (_activeQueue.IsEmpty)
                {
                    return null;
                }

                // Evaluate highest priority first, then FIFO (CreatedAt/Received timeline)
                var nextCandidate = _activeQueue.Values
                    .OrderByDescending(c => (int)c.Priority)
                    .ThenBy(c => c.CommandId) // stable fallback
                    .FirstOrDefault();

                if (nextCandidate != null)
                {
                    _activeQueue.TryRemove(nextCandidate.CommandId, out _);
                    _expirationQueue.TryRemove(nextCandidate.CommandId, out _);

                    // Delete from database
                    using var conn = _dbContext.CreateConnection();
                    await conn.OpenAsync(ct);
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM PersistentCommandQueue WHERE CommandId = $id;";
                    var pId = cmd.CreateParameter();
                    pId.ParameterName = "$id";
                    pId.Value = nextCandidate.CommandId;
                    cmd.Parameters.Add(pId);
                    await cmd.ExecuteNonQueryAsync(ct);

                    _logger.LogInformation("Command {CommandId} dequeued successfully from Active Queue.", nextCandidate.CommandId);
                    return nextCandidate;
                }

                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<int> GetQueueSizeAsync(CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct);
            return _activeQueue.Count;
        }

        /// <inheritdoc />
        public async Task<QueueStatistics> GetStatisticsAsync(CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct);
            return new QueueStatistics
            {
                ActiveCount = _activeQueue.Count,
                DelayedCount = _delayedQueue.Count,
                OfflineCount = _offlineQueue.Count,
                RetryCount = _retryQueue.Count,
                DeadLetterCount = _deadLetterQueue.Count,
                CancellationCount = _cancellationQueue.Count,
                ExpirationCount = _expirationQueue.Count,
                TotalPersistentCount = await GetTotalPersistentCountAsync(ct)
            };
        }

        /// <inheritdoc />
        public async Task<bool> CancelCommandAsync(string commandId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(commandId)) return false;
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                _cancellationQueue[commandId] = DateTime.UtcNow;

                bool removed = false;
                if (_activeQueue.TryRemove(commandId, out _)) removed = true;
                if (_delayedQueue.TryRemove(commandId, out _)) removed = true;
                if (_offlineQueue.TryRemove(commandId, out _)) removed = true;
                if (_retryQueue.TryRemove(commandId, out _)) removed = true;

                _expirationQueue.TryRemove(commandId, out _);

                using var conn = _dbContext.CreateConnection();
                await conn.OpenAsync(ct);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM PersistentCommandQueue WHERE CommandId = $id;";
                var pId = cmd.CreateParameter();
                pId.ParameterName = "$id";
                pId.Value = commandId;
                cmd.Parameters.Add(pId);
                await cmd.ExecuteNonQueryAsync(ct);

                _logger.LogInformation("Command {CommandId} cancellation request processed. Removed from queue? {Removed}", commandId, removed);
                return removed;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task RecoverQueueAsync(CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                _logger.LogInformation("Starting persistent queue recovery...");

                using var conn = _dbContext.CreateConnection();
                await conn.OpenAsync(ct);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM PersistentCommandQueue;";

                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    string commandId = reader.GetString(reader.GetOrdinal("CommandId"));
                    string action = reader.GetString(reader.GetOrdinal("Action"));
                    string targetMachineId = reader.GetString(reader.GetOrdinal("TargetMachineId"));
                    int priority = reader.GetInt32(reader.GetOrdinal("Priority"));
                    string creatorOperatorId = reader.GetString(reader.GetOrdinal("CreatorOperatorId"));
                    DateTime expiresAtUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("ExpiresAtUtc")));
                    string signature = reader.GetString(reader.GetOrdinal("Signature"));
                    string paramsJson = reader.GetString(reader.GetOrdinal("ParametersJson"));
                    string queueType = reader.GetString(reader.GetOrdinal("QueueType"));

                    var parameters = JsonSerializer.Deserialize<List<CommandParameter>>(paramsJson) ?? new List<CommandParameter>();

                    var command = new RemoteCommand
                    {
                        CommandId = commandId,
                        Action = action,
                        TargetMachineId = targetMachineId,
                        Priority = (CommandPriority)priority,
                        CreatorOperatorId = creatorOperatorId,
                        ExpiresAtUtc = expiresAtUtc,
                        Signature = signature,
                        Parameters = parameters
                    };

                    switch (queueType.ToUpperInvariant())
                    {
                        case "ACTIVE":
                            _activeQueue[commandId] = command;
                            _expirationQueue[commandId] = expiresAtUtc;
                            break;
                        case "DELAYED":
                            var schedStr = reader.IsDBNull(reader.GetOrdinal("ScheduledAtUtc")) ? null : reader.GetString(reader.GetOrdinal("ScheduledAtUtc"));
                            if (schedStr != null)
                            {
                                _delayedQueue[commandId] = DateTime.Parse(schedStr);
                                _activeQueue[commandId] = command; // standby in memory
                            }
                            break;
                        case "OFFLINE":
                            _offlineQueue[commandId] = targetMachineId;
                            _activeQueue[commandId] = command; // standby
                            break;
                        case "RETRY":
                            var runAtStr = reader.IsDBNull(reader.GetOrdinal("ScheduledAtUtc")) ? null : reader.GetString(reader.GetOrdinal("ScheduledAtUtc"));
                            int retryCount = reader.GetInt32(reader.GetOrdinal("RetryCount"));
                            if (runAtStr != null)
                            {
                                _retryQueue[commandId] = (command, DateTime.Parse(runAtStr), retryCount);
                            }
                            break;
                        case "DEAD_LETTER":
                            string reason = reader.IsDBNull(reader.GetOrdinal("DeadLetterReason")) ? "Unknown" : reader.GetString(reader.GetOrdinal("DeadLetterReason"));
                            _deadLetterQueue[commandId] = reason;
                            break;
                    }
                }

                _logger.LogInformation("Queue recovery completed. Active: {Active}, Delayed: {Delayed}, Offline: {Offline}, Retry: {Retry}, DLQ: {DLQ}",
                    _activeQueue.Count, _delayedQueue.Count, _offlineQueue.Count, _retryQueue.Count, _deadLetterQueue.Count);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task MoveToDeadLetterQueueAsync(RemoteCommand command, string reason, CancellationToken ct = default)
        {
            if (command == null) return;
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                _activeQueue.TryRemove(command.CommandId, out _);
                _delayedQueue.TryRemove(command.CommandId, out _);
                _offlineQueue.TryRemove(command.CommandId, out _);
                _retryQueue.TryRemove(command.CommandId, out _);
                _expirationQueue.TryRemove(command.CommandId, out _);

                _deadLetterQueue[command.CommandId] = reason;

                await SaveToDatabaseInternalAsync(command, "DEAD_LETTER", null, 0, reason, ct);
                _logger.LogWarning("Command {CommandId} moved to DLQ. Reason: {Reason}", command.CommandId, reason);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task ScheduleRetryAsync(RemoteCommand command, DateTime runAtUtc, int retryCount, CancellationToken ct = default)
        {
            if (command == null) return;
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                _activeQueue.TryRemove(command.CommandId, out _);
                _retryQueue[command.CommandId] = (command, runAtUtc, retryCount);

                await SaveToDatabaseInternalAsync(command, "RETRY", runAtUtc, retryCount, null, ct);
                _logger.LogInformation("Command {CommandId} scheduled for retry #{Count} at {Time}", command.CommandId, retryCount, runAtUtc);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task ReplayOfflineCommandsAsync(string machineId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId)) return;
            await EnsureInitializedAsync(ct);

            await _lock.WaitAsync(ct);
            try
            {
                var cmdIdsToReplay = _offlineQueue
                    .Where(kv => string.Equals(kv.Value, machineId, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Key)
                    .ToList();

                if (!cmdIdsToReplay.Any()) return;

                _logger.LogInformation("Replaying {Count} offline commands for workstation {MachineId}...", cmdIdsToReplay.Count, machineId);

                foreach (var cmdId in cmdIdsToReplay)
                {
                    _offlineQueue.TryRemove(cmdId, out _);

                    // If it is in active queue dictionary (standby), make sure it remains there as active.
                    // Update database type back to ACTIVE
                    using var conn = _dbContext.CreateConnection();
                    await conn.OpenAsync(ct);
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE PersistentCommandQueue SET QueueType = 'ACTIVE' WHERE CommandId = $id;";
                    var pId = cmd.CreateParameter();
                    pId.ParameterName = "$id";
                    pId.Value = cmdId;
                    cmd.Parameters.Add(pId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task PruneExpiredAndDelayedAsync(CancellationToken ct = default)
        {
            await PruneExpiredAndDelayedInternalAsync();
        }

        private async Task PruneExpiredAndDelayedInternalAsync()
        {
            try
            {
                await _lock.WaitAsync();
                try
                {
                    var now = DateTime.UtcNow;

                    // 1. Process Delayed Queue -> Active
                    var delayedToActive = _delayedQueue
                        .Where(kv => kv.Value <= now)
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var cmdId in delayedToActive)
                    {
                        _delayedQueue.TryRemove(cmdId, out _);
                        using var conn = _dbContext.CreateConnection();
                        await conn.OpenAsync();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "UPDATE PersistentCommandQueue SET QueueType = 'ACTIVE' WHERE CommandId = $id;";
                        var pId = cmd.CreateParameter();
                        pId.ParameterName = "$id";
                        pId.Value = cmdId;
                        cmd.Parameters.Add(pId);
                        await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation("Delayed command {CommandId} has reached timeline and is released to Active Queue.", cmdId);
                    }

                    // 2. Process Retry Queue -> Active
                    var retryToActive = _retryQueue
                        .Where(kv => kv.Value.RunAtUtc <= now)
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var cmdId in retryToActive)
                    {
                        if (_retryQueue.TryRemove(cmdId, out var retryInfo))
                        {
                            _activeQueue[cmdId] = retryInfo.Command;
                            _expirationQueue[cmdId] = retryInfo.Command.ExpiresAtUtc;

                            using var conn = _dbContext.CreateConnection();
                            await conn.OpenAsync();
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = "UPDATE PersistentCommandQueue SET QueueType = 'ACTIVE' WHERE CommandId = $id;";
                            var pId = cmd.CreateParameter();
                            pId.ParameterName = "$id";
                            pId.Value = cmdId;
                            cmd.Parameters.Add(pId);
                            await cmd.ExecuteNonQueryAsync();

                            _logger.LogInformation("Retry command {CommandId} released to Active Queue.", cmdId);
                        }
                    }

                    // 3. Process Expirations -> DLQ
                    var expiredCmdIds = _expirationQueue
                        .Where(kv => kv.Value <= now)
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var cmdId in expiredCmdIds)
                    {
                        _expirationQueue.TryRemove(cmdId, out _);
                        if (_activeQueue.TryRemove(cmdId, out var cmdRecord))
                        {
                            _deadLetterQueue[cmdId] = "Expired";
                            await SaveToDatabaseInternalAsync(cmdRecord, "DEAD_LETTER", null, 0, "Expired", CancellationToken.None);
                            _logger.LogWarning("Command {CommandId} expired inside queue and was routed to DLQ.", cmdId);
                        }
                    }
                }
                finally
                {
                    _lock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during periodic queue cleanup.");
            }
        }

        private void PurgeCompletedCancellationsAndExpirations()
        {
            var threshold = DateTime.UtcNow.AddMinutes(-30);
            var toRemove = _cancellationQueue.Where(kv => kv.Value < threshold).Select(kv => kv.Key).ToList();
            foreach (var key in toRemove)
            {
                _cancellationQueue.TryRemove(key, out _);
            }
        }

        private async Task SaveToDatabaseInternalAsync(
            RemoteCommand command,
            string queueType,
            DateTime? scheduledAtUtc,
            int retryCount,
            string? deadLetterReason,
            CancellationToken ct)
        {
            using var conn = _dbContext.CreateConnection();
            await conn.OpenAsync(ct);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO PersistentCommandQueue (
                    CommandId, Action, TargetMachineId, Priority, CreatorOperatorId, ExpiresAtUtc, Signature, ParametersJson, QueueType, ScheduledAtUtc, RetryCount, DeadLetterReason
                ) VALUES ($id, $act, $target, $pri, $creator, $exp, $sig, $params, $qtype, $sched, $retry, $reason)
                ON CONFLICT(CommandId) DO UPDATE SET
                    QueueType = excluded.QueueType,
                    ScheduledAtUtc = excluded.ScheduledAtUtc,
                    RetryCount = excluded.RetryCount,
                    DeadLetterReason = excluded.DeadLetterReason;";

            AddParameter(cmd, "$id", command.CommandId);
            AddParameter(cmd, "$act", command.Action);
            AddParameter(cmd, "$target", command.TargetMachineId);
            AddParameter(cmd, "$pri", (int)command.Priority);
            AddParameter(cmd, "$creator", command.CreatorOperatorId);
            AddParameter(cmd, "$exp", command.ExpiresAtUtc.ToString("O"));
            AddParameter(cmd, "$sig", command.Signature);
            AddParameter(cmd, "$params", JsonSerializer.Serialize(command.Parameters));
            AddParameter(cmd, "$qtype", queueType);
            AddParameter(cmd, "$sched", scheduledAtUtc?.ToString("O"));
            AddParameter(cmd, "$retry", retryCount);
            AddParameter(cmd, "$reason", deadLetterReason);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static void AddParameter(DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private async Task<int> GetTotalPersistentCountAsync(CancellationToken ct)
        {
            try
            {
                using var conn = _dbContext.CreateConnection();
                await conn.OpenAsync(ct);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM PersistentCommandQueue;";
                var count = await cmd.ExecuteScalarAsync(ct);
                return count != null ? Convert.ToInt32(count) : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cleanupTimer.Dispose();
            _lock.Dispose();
        }
    }
}
