using System;
using System.Collections.Generic;
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
    /// Thread-safe, SQLCipher-encrypted repository implementation for Rollback History logs.
    /// </summary>
    public class RollbackHistoryRepository : IRollbackHistoryRepository
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public RollbackHistoryRepository(
            IOptions<StorageOptions> storageOptions,
            ICryptographyService? cryptographyService = null)
        {
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

        public async Task InsertAsync(RollbackHistoryRecord record, CancellationToken cancellationToken = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO RollbackLogs (
                        Id, Reason, TriggerSource, PreviousVersion, RestoredVersion,
                        DurationSeconds, Result, FailureDetails, Timestamp
                    ) VALUES (
                        $id, $reason, $source, $prevVersion, $restoredVersion,
                        $duration, $result, $details, $timestamp
                    );";

                cmd.Parameters.AddWithValue("$id", record.Id.ToString());
                cmd.Parameters.AddWithValue("$reason", record.Reason ?? string.Empty);
                cmd.Parameters.AddWithValue("$source", record.TriggerSource ?? string.Empty);
                cmd.Parameters.AddWithValue("$prevVersion", record.PreviousVersion ?? string.Empty);
                cmd.Parameters.AddWithValue("$restoredVersion", record.RestoredVersion ?? string.Empty);
                cmd.Parameters.AddWithValue("$duration", (int)record.Duration.TotalSeconds);
                cmd.Parameters.AddWithValue("$result", record.Result ?? string.Empty);
                cmd.Parameters.AddWithValue("$details", record.FailureDetails ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$timestamp", record.Timestamp.ToString("O"));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new HistoryPersistenceException($"Failed to insert rollback history record with ID {record.Id}.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<RollbackHistoryRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM RollbackLogs WHERE Id = $id;";
                cmd.Parameters.AddWithValue("$id", id.ToString());

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    return ReadRecord(reader);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new HistoryPersistenceException($"Failed to retrieve rollback history record with ID {id}.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IEnumerable<RollbackHistoryRecord>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                var list = new List<RollbackHistoryRecord>();
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM RollbackLogs ORDER BY Timestamp DESC;";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(ReadRecord(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                throw new HistoryPersistenceException("Failed to retrieve all rollback history records.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task CleanupAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM RollbackLogs WHERE Timestamp < $before;";
                cmd.Parameters.AddWithValue("$before", beforeUtc.ToString("O"));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new HistoryPersistenceException($"Failed to clean up rollback history records before {beforeUtc}.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        private static RollbackHistoryRecord ReadRecord(SqliteDataReader reader)
        {
            return new RollbackHistoryRecord
            {
                Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                TriggerSource = reader.GetString(reader.GetOrdinal("TriggerSource")),
                PreviousVersion = reader.GetString(reader.GetOrdinal("PreviousVersion")),
                RestoredVersion = reader.GetString(reader.GetOrdinal("RestoredVersion")),
                Duration = TimeSpan.FromSeconds(reader.GetInt32(reader.GetOrdinal("DurationSeconds"))),
                Result = reader.GetString(reader.GetOrdinal("Result")),
                FailureDetails = reader.IsDBNull(reader.GetOrdinal("FailureDetails")) ? string.Empty : reader.GetString(reader.GetOrdinal("FailureDetails")),
                Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("Timestamp")))
            };
        }
    }
}
