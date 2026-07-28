using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;
using Sayra.Client.Shared.Security.Crypto;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Thread-safe, SQLCipher-encrypted offline queue repository for storing unsent telemetry events.
    /// </summary>
    public class TelemetryOfflineQueue : ITelemetryOfflineQueue
    {
        private readonly string _connectionString;
        private readonly TelemetryOptions _telemetryOptions;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _tableCreated;

        public TelemetryOfflineQueue(
            IOptions<StorageOptions> storageOptions,
            IOptions<TelemetryOptions> telemetryOptions,
            ICryptographyService? cryptographyService = null)
        {
            _telemetryOptions = telemetryOptions.Value;

            var options = storageOptions.Value;
            string dbPath;
            if (string.IsNullOrEmpty(options.DatabasePath))
            {
                dbPath = Path.Combine(AppContext.BaseDirectory, "Data", "update_platform.db");
            }
            else
            {
                dbPath = options.DatabasePath;
            }

            var connBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Cache = SqliteCacheMode.Private,
                Password = DatabaseKeyManager.GetOrInitializeKey(cryptographyService),
                Pooling = false
            };
            _connectionString = connBuilder.ConnectionString;
        }

        private async Task EnsureTableCreatedAsync(CancellationToken cancellationToken)
        {
            if (_tableCreated) return;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TelemetryEvents (
                    EventId TEXT PRIMARY KEY NOT NULL,
                    EventType TEXT NOT NULL,
                    TimestampUtc TEXT NOT NULL,
                    CorrelationId TEXT NOT NULL,
                    SourceVersion TEXT NOT NULL,
                    TargetVersion TEXT NOT NULL,
                    Success INTEGER NOT NULL,
                    ErrorCode TEXT NOT NULL,
                    ErrorMessage TEXT NOT NULL,
                    DeviceIdentifier TEXT NOT NULL,
                    PayloadJson TEXT NOT NULL,
                    AttemptCount INTEGER DEFAULT 0
                );";

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _tableCreated = true;
        }

        public async Task EnqueueAsync(UpdateTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
        {
            if (telemetryEvent == null) throw new ArgumentNullException(nameof(telemetryEvent));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                await EnsureTableCreatedAsync(cancellationToken);

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                // Enforce queue limits before inserting
                int count = await GetCountInternalAsync(connection, cancellationToken);
                if (count >= _telemetryOptions.QueueLimit)
                {
                    // Enforce limit by purging oldest events (FIFO)
                    int toDelete = (count - _telemetryOptions.QueueLimit) + 1;
                    using var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = @"
                        DELETE FROM TelemetryEvents
                        WHERE EventId IN (
                            SELECT EventId FROM TelemetryEvents
                            ORDER BY TimestampUtc ASC
                            LIMIT $limit
                        );";
                    deleteCmd.Parameters.AddWithValue("$limit", toDelete);
                    await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO TelemetryEvents (
                        EventId, EventType, TimestampUtc, CorrelationId, SourceVersion, TargetVersion,
                        Success, ErrorCode, ErrorMessage, DeviceIdentifier, PayloadJson, AttemptCount
                    ) VALUES (
                        $id, $type, $time, $corrId, $src, $tgt, $success, $errCode, $errMsg, $device, $payload, 0
                    );";

                insertCmd.Parameters.AddWithValue("$id", telemetryEvent.EventId.ToString());
                insertCmd.Parameters.AddWithValue("$type", telemetryEvent.EventType);
                insertCmd.Parameters.AddWithValue("$time", telemetryEvent.TimestampUtc.ToString("O"));
                insertCmd.Parameters.AddWithValue("$corrId", telemetryEvent.CorrelationId ?? string.Empty);
                insertCmd.Parameters.AddWithValue("$src", telemetryEvent.SourceVersion ?? string.Empty);
                insertCmd.Parameters.AddWithValue("$tgt", telemetryEvent.TargetVersion ?? string.Empty);
                insertCmd.Parameters.AddWithValue("$success", telemetryEvent.Success ? 1 : 0);
                insertCmd.Parameters.AddWithValue("$errCode", telemetryEvent.ErrorCode ?? string.Empty);
                insertCmd.Parameters.AddWithValue("$errMsg", telemetryEvent.ErrorMessage ?? string.Empty);
                insertCmd.Parameters.AddWithValue("$device", telemetryEvent.DeviceIdentifier ?? string.Empty);
                insertCmd.Parameters.AddWithValue("$payload", telemetryEvent.PayloadJson ?? string.Empty);

                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new ReportingQueueException("Failed to buffer telemetry event locally.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IEnumerable<UpdateTelemetryEvent>> DequeueBatchAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            if (batchSize <= 0) throw new ArgumentException("Batch size must be greater than zero.", nameof(batchSize));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                await EnsureTableCreatedAsync(cancellationToken);

                var list = new List<UpdateTelemetryEvent>();
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM TelemetryEvents ORDER BY TimestampUtc ASC LIMIT $limit;";
                cmd.Parameters.AddWithValue("$limit", batchSize);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(new UpdateTelemetryEvent
                    {
                        EventId = Guid.Parse(reader.GetString(reader.GetOrdinal("EventId"))),
                        EventType = reader.GetString(reader.GetOrdinal("EventType")),
                        TimestampUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("TimestampUtc"))),
                        CorrelationId = reader.GetString(reader.GetOrdinal("CorrelationId")),
                        SourceVersion = reader.GetString(reader.GetOrdinal("SourceVersion")),
                        TargetVersion = reader.GetString(reader.GetOrdinal("TargetVersion")),
                        Success = reader.GetInt32(reader.GetOrdinal("Success")) == 1,
                        ErrorCode = reader.GetString(reader.GetOrdinal("ErrorCode")),
                        ErrorMessage = reader.GetString(reader.GetOrdinal("ErrorMessage")),
                        DeviceIdentifier = reader.GetString(reader.GetOrdinal("DeviceIdentifier")),
                        PayloadJson = reader.GetString(reader.GetOrdinal("PayloadJson"))
                    });
                }

                return list;
            }
            catch (Exception ex)
            {
                throw new ReportingQueueException("Failed to retrieve buffered telemetry events.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task DeleteBatchAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default)
        {
            if (eventIds == null) throw new ArgumentNullException(nameof(eventIds));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                await EnsureTableCreatedAsync(cancellationToken);

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();
                try
                {
                    foreach (var id in eventIds)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM TelemetryEvents WHERE EventId = $id;";
                        cmd.Parameters.AddWithValue("$id", id.ToString());
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw new ReportingQueueException("Failed to delete processed telemetry events from offline buffer.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await EnsureTableCreatedAsync(cancellationToken);

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                return await GetCountInternalAsync(connection, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new ReportingQueueException("Failed to query buffered event count.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        private static async Task<int> GetCountInternalAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM TelemetryEvents;";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        public async Task IncrementAttemptCountAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default)
        {
            if (eventIds == null) throw new ArgumentNullException(nameof(eventIds));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                await EnsureTableCreatedAsync(cancellationToken);

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();
                try
                {
                    foreach (var id in eventIds)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = "UPDATE TelemetryEvents SET AttemptCount = AttemptCount + 1 WHERE EventId = $id;";
                        cmd.Parameters.AddWithValue("$id", id.ToString());
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw new ReportingQueueException("Failed to increment retry attempt counters.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
