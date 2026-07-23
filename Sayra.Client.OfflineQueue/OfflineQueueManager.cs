using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sayra.Client.OfflineQueue.Models;
using Sayra.Client.OfflineQueue.Security;
using Sayra.Client.OfflineQueue.Serialization;

namespace Sayra.Client.OfflineQueue;

public class OfflineQueueManager : IOfflineQueueManager, IDisposable
{
    private readonly ILogger<OfflineQueueManager> _logger;
    private readonly IEventSerializer _serializer;
    private readonly IQueueSecurityManager _securityManager;
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _isDisposed;

    public OfflineQueueManager(
        ILogger<OfflineQueueManager> logger,
        IEventSerializer serializer,
        IQueueSecurityManager securityManager,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _securityManager = securityManager ?? throw new ArgumentNullException(nameof(securityManager));

        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        _dbPath = Path.Combine(dataDir, "offline_queue.db");
        _connectionString = $"Data Source={_dbPath};Cache=Shared";

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        _dbLock.Wait();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Enable WAL mode for high performance and transaction safety
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=WAL;";
                command.ExecuteNonQuery();
            }

            // Create QueueItem Table
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS QueueItem (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        EventType TEXT NOT NULL,
                        Payload TEXT NOT NULL,
                        Priority INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        RetryCount INTEGER NOT NULL DEFAULT 0,
                        Status TEXT NOT NULL,
                        LastAttemptAt TEXT,
                        ErrorMessage TEXT
                    );";
                command.ExecuteNonQuery();
            }

            // Create DeadLetterQueue Table
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS DeadLetterQueue (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        OriginalQueueItemId INTEGER NOT NULL,
                        EventType TEXT NOT NULL,
                        Payload TEXT NOT NULL,
                        Priority INTEGER NOT NULL,
                        ErrorReason TEXT,
                        RetryHistory TEXT,
                        Timestamp TEXT NOT NULL
                    );";
                command.ExecuteNonQuery();
            }

            // Create Indexes
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE INDEX IF NOT EXISTS IX_QueueItem_Status_Priority_CreatedAt ON QueueItem (Status, Priority, CreatedAt);";
                command.ExecuteNonQuery();
            }

            _logger.LogInformation("SQLite Offline Queue storage initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQLite offline queue database. Attempting recovery...");
            _dbLock.Release();
            // Let the recovery mechanism handle it if corruption is detected
            Task.Run(async () => await HandleCorruptionAsync()).Wait();
            return;
        }
        finally
        {
            if (_dbLock.CurrentCount == 0)
            {
                _dbLock.Release();
            }
        }
    }

    public async Task AddEventAsync(ClientEvent clientEvent)
    {
        if (clientEvent == null) throw new ArgumentNullException(nameof(clientEvent));
        clientEvent.Validate();

        await _dbLock.WaitAsync();
        try
        {
            // Serialize full event
            var serializedEvent = _serializer.Serialize(clientEvent);

            // Encrypt serialized payload
            var encryptedPayload = _securityManager.EncryptPayload(serializedEvent);

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO QueueItem (EventType, Payload, Priority, CreatedAt, RetryCount, Status)
                    VALUES ($eventType, $payload, $priority, $createdAt, 0, 'Pending');";

                command.Parameters.AddWithValue("$eventType", clientEvent.EventType);
                command.Parameters.AddWithValue("$payload", encryptedPayload);
                command.Parameters.AddWithValue("$priority", (int)clientEvent.Priority);
                command.Parameters.AddWithValue("$createdAt", clientEvent.CreatedAt.ToString("O"));

                await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();

                _logger.LogDebug("Successfully queued event '{EventType}' with priority {Priority} in SQLite storage.", clientEvent.EventType, clientEvent.Priority);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (SqliteException ex) when (IsCorrupted(ex))
        {
            _logger.LogError(ex, "Database corruption detected during AddEventAsync. Triggering recovery...");
            _dbLock.Release();
            await HandleCorruptionAsync();
            throw;
        }
        finally
        {
            if (_dbLock.CurrentCount == 0) _dbLock.Release();
        }
    }

    public async Task<List<QueueItem>> GetPendingEventsAsync(int limit = 100)
    {
        var result = new List<QueueItem>();

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, EventType, Payload, Priority, CreatedAt, RetryCount, Status, LastAttemptAt, ErrorMessage
                FROM QueueItem
                WHERE Status IN ('Pending', 'Failed')
                ORDER BY Priority DESC, CreatedAt ASC, Id ASC;";

            using var reader = await command.ExecuteReaderAsync();
            var now = DateTime.UtcNow;

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt64(0);
                var eventType = reader.GetString(1);
                var payload = reader.GetString(2);
                var priority = (QueuePriority)reader.GetInt32(3);
                var createdAt = DateTime.Parse(reader.GetString(4));
                var retryCount = reader.GetInt32(5);
                var status = reader.GetString(6);

                DateTime? lastAttemptAt = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7));
                string? errorMessage = reader.IsDBNull(8) ? null : reader.GetString(8);

                // Check backoff restriction
                if (status == "Failed" && lastAttemptAt.HasValue)
                {
                    var backoff = GetBackoffDelay(retryCount);
                    if (now < lastAttemptAt.Value.Add(backoff))
                    {
                        // Skip this item as its backoff window has not expired yet
                        continue;
                    }
                }

                result.Add(new QueueItem
                {
                    Id = id,
                    EventType = eventType,
                    Payload = payload,
                    Priority = priority,
                    CreatedAt = createdAt,
                    RetryCount = retryCount,
                    Status = status,
                    LastAttemptAt = lastAttemptAt,
                    ErrorMessage = errorMessage
                });

                if (result.Count >= limit)
                {
                    break;
                }
            }
        }
        catch (SqliteException ex) when (IsCorrupted(ex))
        {
            _logger.LogError(ex, "Database corruption detected during GetPendingEventsAsync. Triggering recovery...");
            _dbLock.Release();
            await HandleCorruptionAsync();
            return new List<QueueItem>();
        }
        finally
        {
            if (_dbLock.CurrentCount == 0) _dbLock.Release();
        }

        return result;
    }

    public async Task MarkCompletedAsync(long id)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    UPDATE QueueItem
                    SET Status = 'Completed', LastAttemptAt = $lastAttempt, ErrorMessage = NULL
                    WHERE Id = $id;";
                command.Parameters.AddWithValue("$lastAttempt", DateTime.UtcNow.ToString("O"));
                command.Parameters.AddWithValue("$id", id);

                await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();

                _logger.LogDebug("Queue item {QueueItemId} marked as Completed.", id);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (SqliteException ex) when (IsCorrupted(ex))
        {
            _logger.LogError(ex, "Database corruption detected during MarkCompletedAsync. Triggering recovery...");
            _dbLock.Release();
            await HandleCorruptionAsync();
            throw;
        }
        finally
        {
            if (_dbLock.CurrentCount == 0) _dbLock.Release();
        }
    }

    public async Task RecordFailureAsync(long id, string errorMessage, int maxRetries = 5)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Load the current item details to decide on DLQ promotion
            QueueItem? item = null;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT EventType, Payload, Priority, CreatedAt, RetryCount FROM QueueItem WHERE Id = $id;";
                command.Parameters.AddWithValue("$id", id);
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    item = new QueueItem
                    {
                        Id = id,
                        EventType = reader.GetString(0),
                        Payload = reader.GetString(1),
                        Priority = (QueuePriority)reader.GetInt32(2),
                        CreatedAt = DateTime.Parse(reader.GetString(3)),
                        RetryCount = reader.GetInt32(4)
                    };
                }
            }

            if (item == null)
            {
                _logger.LogWarning("Attempted to record failure on non-existent QueueItem {QueueItemId}.", id);
                return;
            }

            int newRetryCount = item.RetryCount + 1;
            using var transaction = connection.BeginTransaction();
            try
            {
                if (newRetryCount >= maxRetries)
                {
                    // Move to Dead Letter Queue
                    _logger.LogWarning("Queue item {QueueItemId} exceeded max retries ({MaxRetries}). Routing to Dead Letter Queue (DLQ)...", id, maxRetries);

                    var retryHistory = $"Attempt 1-{newRetryCount} failed. Last error: {errorMessage}";

                    using (var dlqCommand = connection.CreateCommand())
                    {
                        dlqCommand.Transaction = transaction;
                        dlqCommand.CommandText = @"
                            INSERT INTO DeadLetterQueue (OriginalQueueItemId, EventType, Payload, Priority, ErrorReason, RetryHistory, Timestamp)
                            VALUES ($origId, $eventType, $payload, $priority, $errorReason, $history, $timestamp);";

                        dlqCommand.Parameters.AddWithValue("$origId", id);
                        dlqCommand.Parameters.AddWithValue("$eventType", item.EventType);
                        dlqCommand.Parameters.AddWithValue("$payload", item.Payload);
                        dlqCommand.Parameters.AddWithValue("$priority", (int)item.Priority);
                        dlqCommand.Parameters.AddWithValue("$errorReason", errorMessage);
                        dlqCommand.Parameters.AddWithValue("$history", retryHistory);
                        dlqCommand.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));

                        await dlqCommand.ExecuteNonQueryAsync();
                    }

                    // Delete from active queue
                    using (var delCommand = connection.CreateCommand())
                    {
                        delCommand.Transaction = transaction;
                        delCommand.CommandText = "DELETE FROM QueueItem WHERE Id = $id;";
                        delCommand.Parameters.AddWithValue("$id", id);
                        await delCommand.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // Update main table
                    using (var updateCommand = connection.CreateCommand())
                    {
                        updateCommand.Transaction = transaction;
                        updateCommand.CommandText = @"
                            UPDATE QueueItem
                            SET Status = 'Failed', RetryCount = $retry, LastAttemptAt = $lastAttempt, ErrorMessage = $error
                            WHERE Id = $id;";
                        updateCommand.Parameters.AddWithValue("$retry", newRetryCount);
                        updateCommand.Parameters.AddWithValue("$lastAttempt", DateTime.UtcNow.ToString("O"));
                        updateCommand.Parameters.AddWithValue("$error", errorMessage);
                        updateCommand.Parameters.AddWithValue("$id", id);

                        await updateCommand.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (SqliteException ex) when (IsCorrupted(ex))
        {
            _logger.LogError(ex, "Database corruption detected during RecordFailureAsync. Triggering recovery...");
            _dbLock.Release();
            await HandleCorruptionAsync();
            throw;
        }
        finally
        {
            if (_dbLock.CurrentCount == 0) _dbLock.Release();
        }
    }

    public async Task DeleteExpiredEventsAsync(TimeSpan maxAge)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                var thresholdDate = DateTime.UtcNow.Subtract(maxAge).ToString("O");

                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    DELETE FROM QueueItem
                    WHERE Status = 'Completed' AND LastAttemptAt < $thresholdDate;";
                command.Parameters.AddWithValue("$thresholdDate", thresholdDate);

                int rowsDeleted = await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();

                if (rowsDeleted > 0)
                {
                    _logger.LogInformation("Pruned {Count} expired completed events from the queue.", rowsDeleted);
                }
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (SqliteException ex) when (IsCorrupted(ex))
        {
            _logger.LogError(ex, "Database corruption detected during DeleteExpiredEventsAsync. Triggering recovery...");
            _dbLock.Release();
            await HandleCorruptionAsync();
            throw;
        }
        finally
        {
            if (_dbLock.CurrentCount == 0) _dbLock.Release();
        }
    }

    public async Task<List<DeadLetterQueueItem>> GetDeadLetterItemsAsync()
    {
        var result = new List<DeadLetterQueueItem>();

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, OriginalQueueItemId, EventType, Payload, Priority, ErrorReason, RetryHistory, Timestamp
                FROM DeadLetterQueue
                ORDER BY Id DESC;";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new DeadLetterQueueItem
                {
                    Id = reader.GetInt64(0),
                    OriginalQueueItemId = reader.GetInt64(1),
                    EventType = reader.GetString(2),
                    Payload = reader.GetString(3),
                    Priority = (QueuePriority)reader.GetInt32(4),
                    ErrorReason = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    RetryHistory = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Timestamp = DateTime.Parse(reader.GetString(7))
                });
            }
        }
        catch (SqliteException ex) when (IsCorrupted(ex))
        {
            _logger.LogError(ex, "Database corruption detected during GetDeadLetterItemsAsync. Triggering recovery...");
            _dbLock.Release();
            await HandleCorruptionAsync();
            return new List<DeadLetterQueueItem>();
        }
        finally
        {
            if (_dbLock.CurrentCount == 0) _dbLock.Release();
        }

        return result;
    }

    public async Task<long> GetQueueSizeInBytesAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    return new FileInfo(_dbPath).Length;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch database file size.");
            }
            return 0L;
        });
    }

    public async Task<int> GetPendingCountAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM QueueItem WHERE Status IN ('Pending', 'Failed');";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        catch (SqliteException ex) when (IsCorrupted(ex))
        {
            _logger.LogError(ex, "Database corruption detected during GetPendingCountAsync. Triggering recovery...");
            _dbLock.Release();
            await HandleCorruptionAsync();
            return 0;
        }
        finally
        {
            if (_dbLock.CurrentCount == 0) _dbLock.Release();
        }
    }

    public async Task<bool> VerifyIntegrityAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            if (!File.Exists(_dbPath)) return true;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            var result = await command.ExecuteScalarAsync() as string;

            bool isHealthy = string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
            if (!isHealthy)
            {
                _logger.LogCritical("PRAGMA integrity_check failed on queue database. Result: {Result}", result);
            }
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during PRAGMA integrity check on the queue database.");
            return false;
        }
        finally
        {
            if (_dbLock.CurrentCount == 0) _dbLock.Release();
        }
    }

    public async Task ForceRecreateDatabaseAsync()
    {
        _logger.LogWarning("ForceRecreateDatabaseAsync requested. Proceeding with database recreation...");
        await HandleCorruptionAsync();
    }

    private async Task HandleCorruptionAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            _logger.LogWarning("CRITICAL: Re-creating queue database. Backing up the old file first...");

            // Force close connections by running SQLite garbage collection
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (File.Exists(_dbPath))
            {
                var backupPath = $"{_dbPath}.corrupted.{DateTime.UtcNow:yyyyMMddHHmmss}";
                try
                {
                    File.Move(_dbPath, backupPath, overwrite: true);
                    _logger.LogWarning("Corrupted database file moved to '{Path}' for offline analysis.", backupPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move corrupted database. Deleting instead...");
                    try
                    {
                        File.Delete(_dbPath);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogError(deleteEx, "Failed to delete corrupted database file!");
                    }
                }
            }

            // Re-initialize tables
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS QueueItem (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    EventType TEXT NOT NULL,
                    Payload TEXT NOT NULL,
                    Priority INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    RetryCount INTEGER NOT NULL DEFAULT 0,
                    Status TEXT NOT NULL,
                    LastAttemptAt TEXT,
                    ErrorMessage TEXT
                );
                CREATE TABLE IF NOT EXISTS DeadLetterQueue (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OriginalQueueItemId INTEGER NOT NULL,
                    EventType TEXT NOT NULL,
                    Payload TEXT NOT NULL,
                    Priority INTEGER NOT NULL,
                    ErrorReason TEXT,
                    RetryHistory TEXT,
                    Timestamp TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_QueueItem_Status_Priority_CreatedAt ON QueueItem (Status, Priority, CreatedAt);
                PRAGMA journal_mode=WAL;";
            await command.ExecuteNonQueryAsync();

            _logger.LogInformation("Fresh database schema recreated successfully after corruption.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate queue database during corruption recovery!");
        }
        finally
        {
            if (_dbLock.CurrentCount == 0) _dbLock.Release();
        }
    }

    private static bool IsCorrupted(SqliteException ex)
    {
        return ex.SqliteErrorCode == 11 || // SQLITE_CORRUPT
               ex.Message.Contains("corrupt", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("malformed", StringComparison.OrdinalIgnoreCase);
    }

    public static TimeSpan GetBackoffDelay(int retryCount)
    {
        if (retryCount <= 0) return TimeSpan.Zero;
        double seconds = Math.Min(300, Math.Pow(3, retryCount));
        return TimeSpan.FromSeconds(seconds);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _dbLock.Dispose();
        SqliteConnection.ClearAllPools();
    }
}
