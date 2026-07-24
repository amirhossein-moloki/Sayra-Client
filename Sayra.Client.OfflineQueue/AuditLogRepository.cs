using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.OfflineQueue
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly SemaphoreSlim _dbLock = new(1, 1);

        public AuditLogRepository(string dbName = "offline_queue.db")
        {
            var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            _dbPath = Path.Combine(dataDir, dbName);
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

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA journal_mode=WAL;";
                    command.ExecuteNonQuery();
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS AuditLogs (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            EventId TEXT NOT NULL UNIQUE,
                            CorrelationId TEXT NOT NULL,
                            SessionId TEXT,
                            TraceId TEXT NOT NULL,
                            Category TEXT NOT NULL,
                            Severity TEXT NOT NULL,
                            MessageTemplate TEXT NOT NULL,
                            PayloadFields TEXT NOT NULL,
                            Timestamp TEXT NOT NULL,
                            RowHash TEXT -- Chained verification hash for anti-tamper compliance
                        );";
                    command.ExecuteNonQuery();
                }

                try
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "ALTER TABLE AuditLogs ADD COLUMN RowHash TEXT;";
                        command.ExecuteNonQuery();
                    }
                }
                catch
                {
                    // Ignore error if column already exists
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE INDEX IF NOT EXISTS IX_AuditLogs_Category_Severity ON AuditLogs (Category, Severity);";
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                _dbLock.Release();
            }
        }

        private async Task<string> GetPreviousRowHashAsync(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT RowHash FROM AuditLogs ORDER BY Id DESC LIMIT 1;";
            var result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? "INITIAL_GENESIS_HASH";
        }

        private string ComputeRowHash(Guid eventId, string timestampStr, string payloadFieldsJson, string previousHash)
        {
            var rawInput = $"{eventId}{timestampStr}{payloadFieldsJson}{previousHash}";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawInput));
            return Convert.ToHexString(hashBytes);
        }

        public async Task AddLogAsync(EventLogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // 1. Get previous row hash to construct cryptographic chain
                string previousHash = await GetPreviousRowHashAsync(connection);

                // 2. Compute RowHash: SHA256(EventId + Timestamp + Payload + PreviousRowHash)
                string payloadFieldsJson = JsonSerializer.Serialize(entry.PayloadFields);
                string timestampStr = entry.Timestamp.ToString("O");
                string rowHash = ComputeRowHash(entry.EventId, timestampStr, payloadFieldsJson, previousHash);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR IGNORE INTO AuditLogs (EventId, CorrelationId, SessionId, TraceId, Category, Severity, MessageTemplate, PayloadFields, Timestamp, RowHash)
                    VALUES ($eventId, $correlationId, $sessionId, $traceId, $category, $severity, $messageTemplate, $payloadFields, $timestamp, $rowHash);";

                command.Parameters.AddWithValue("$eventId", entry.EventId.ToString());
                command.Parameters.AddWithValue("$correlationId", entry.CorrelationId);
                command.Parameters.AddWithValue("$sessionId", (object?)entry.SessionId ?? DBNull.Value);
                command.Parameters.AddWithValue("$traceId", entry.TraceId);
                command.Parameters.AddWithValue("$category", entry.Category);
                command.Parameters.AddWithValue("$severity", entry.Severity);
                command.Parameters.AddWithValue("$messageTemplate", entry.MessageTemplate);
                command.Parameters.AddWithValue("$payloadFields", payloadFieldsJson);
                command.Parameters.AddWithValue("$timestamp", timestampStr);
                command.Parameters.AddWithValue("$rowHash", rowHash);

                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task<List<EventLogEntry>> GetPendingLogsAsync(int limit = 100)
        {
            var result = new List<EventLogEntry>();

            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT EventId, CorrelationId, SessionId, TraceId, Category, Severity, MessageTemplate, PayloadFields, Timestamp, RowHash
                    FROM AuditLogs
                    ORDER BY Id ASC
                    LIMIT $limit;";
                command.Parameters.AddWithValue("$limit", limit);

                string expectedPrevHash = "INITIAL_GENESIS_HASH";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var eventId = Guid.Parse(reader.GetString(0));
                    var correlationId = reader.GetString(1);
                    var sessionId = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var traceId = reader.GetString(3);
                    var category = reader.GetString(4);
                    var severity = reader.GetString(5);
                    var messageTemplate = reader.GetString(6);
                    var payloadFieldsJson = reader.GetString(7);
                    var timestamp = DateTime.Parse(reader.GetString(8));
                    var rowHash = reader.IsDBNull(9) ? null : reader.GetString(9);

                    var payloadFields = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadFieldsJson) ?? new();

                    var entry = new EventLogEntry
                    {
                        EventId = eventId,
                        CorrelationId = correlationId,
                        SessionId = sessionId,
                        TraceId = traceId,
                        Category = category,
                        Severity = severity,
                        MessageTemplate = messageTemplate,
                        PayloadFields = payloadFields,
                        Timestamp = timestamp
                    };

                    // Dynamic signature/chain validation (strictly enforce that verification hash cannot be NULL to prevent bypass attacks)
                    if (rowHash == null)
                    {
                        throw new System.Security.SecurityException($"Audit log tampering detected! Missing cryptographic verification hash (RowHash is NULL) for EventId: {eventId}.");
                    }
                    else
                    {
                        string computed = ComputeRowHash(eventId, reader.GetString(8), payloadFieldsJson, expectedPrevHash);
                        if (computed != rowHash)
                        {
                            throw new System.Security.SecurityException($"Audit log tampering detected! Integrity validation failed for EventId: {eventId}. Computed: {computed}, Stored: {rowHash}");
                        }
                        expectedPrevHash = rowHash;
                    }

                    result.Add(entry);
                }
            }
            finally
            {
                _dbLock.Release();
            }

            return result;
        }

        public async Task DeleteLogsAsync(List<Guid> eventIds)
        {
            if (eventIds == null || eventIds.Count == 0) return;

            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();
                try
                {
                    foreach (var id in eventIds)
                    {
                        using var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM AuditLogs WHERE EventId = $id;";
                        command.Parameters.AddWithValue("$id", id.ToString());
                        await command.ExecuteNonQueryAsync();
                    }
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task<int> GetPendingLogsCountAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM AuditLogs;";
                return Convert.ToInt32(await command.ExecuteScalarAsync());
            }
            finally
            {
                _dbLock.Release();
            }
        }
    }
}
