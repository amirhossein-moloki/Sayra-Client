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
    /// Thread-safe, SQLCipher-encrypted repository implementation for Update History.
    /// </summary>
    public class UpdateHistoryRepository : IUpdateHistoryRepository
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public UpdateHistoryRepository(
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

        public async Task InsertAsync(UpdateHistoryRecord record, CancellationToken cancellationToken = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO UpdateHistory (
                        Id, PackageId, Version, PreviousVersion, InstallationTime,
                        CompletionTime, Status, DurationSeconds, ErrorCode, Result, DeviceIdentifier, TelemetryUploaded
                    ) VALUES (
                        $id, $packageId, $version, $prevVersion, $installTime,
                        $compTime, $status, $duration, $errorCode, $result, $device, $telemetry
                    );";

                cmd.Parameters.AddWithValue("$id", record.Id.ToString());
                cmd.Parameters.AddWithValue("$packageId", record.PackageId.ToString());
                cmd.Parameters.AddWithValue("$version", record.Version);
                cmd.Parameters.AddWithValue("$prevVersion", record.PreviousVersion);
                cmd.Parameters.AddWithValue("$installTime", record.InstallationTime.ToString("O"));
                cmd.Parameters.AddWithValue("$compTime", record.CompletionTime.HasValue ? record.CompletionTime.Value.ToString("O") : DBNull.Value);
                cmd.Parameters.AddWithValue("$status", record.Status);
                cmd.Parameters.AddWithValue("$duration", (int)record.Duration.TotalSeconds);
                cmd.Parameters.AddWithValue("$errorCode", record.ErrorCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$result", record.Result ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$device", record.DeviceIdentifier);
                cmd.Parameters.AddWithValue("$telemetry", record.TelemetryUploaded ? 1 : 0);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new HistoryPersistenceException($"Failed to insert update history record with ID {record.Id}.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task UpdateAsync(UpdateHistoryRecord record, CancellationToken cancellationToken = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE UpdateHistory SET
                        PackageId = $packageId,
                        Version = $version,
                        PreviousVersion = $prevVersion,
                        InstallationTime = $installTime,
                        CompletionTime = $compTime,
                        Status = $status,
                        DurationSeconds = $duration,
                        ErrorCode = $errorCode,
                        Result = $result,
                        DeviceIdentifier = $device,
                        TelemetryUploaded = $telemetry
                    WHERE Id = $id;";

                cmd.Parameters.AddWithValue("$id", record.Id.ToString());
                cmd.Parameters.AddWithValue("$packageId", record.PackageId.ToString());
                cmd.Parameters.AddWithValue("$version", record.Version);
                cmd.Parameters.AddWithValue("$prevVersion", record.PreviousVersion);
                cmd.Parameters.AddWithValue("$installTime", record.InstallationTime.ToString("O"));
                cmd.Parameters.AddWithValue("$compTime", record.CompletionTime.HasValue ? record.CompletionTime.Value.ToString("O") : DBNull.Value);
                cmd.Parameters.AddWithValue("$status", record.Status);
                cmd.Parameters.AddWithValue("$duration", (int)record.Duration.TotalSeconds);
                cmd.Parameters.AddWithValue("$errorCode", record.ErrorCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$result", record.Result ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$device", record.DeviceIdentifier);
                cmd.Parameters.AddWithValue("$telemetry", record.TelemetryUploaded ? 1 : 0);

                int rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                if (rows == 0)
                {
                    throw new HistoryPersistenceException($"UpdateHistory entry with ID {record.Id} not found to update.");
                }
            }
            catch (Exception ex) when (!(ex is HistoryPersistenceException))
            {
                throw new HistoryPersistenceException($"Failed to update history record with ID {record.Id}.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<UpdateHistoryRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM UpdateHistory WHERE Id = $id;";
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
                throw new HistoryPersistenceException($"Failed to retrieve update history record with ID {id}.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IEnumerable<UpdateHistoryRecord>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                var list = new List<UpdateHistoryRecord>();
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM UpdateHistory ORDER BY InstallationTime DESC;";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(ReadRecord(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                throw new HistoryPersistenceException("Failed to retrieve all update history records.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<UpdateHistoryRecord?> GetLatestAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM UpdateHistory ORDER BY InstallationTime DESC LIMIT 1;";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    return ReadRecord(reader);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new HistoryPersistenceException("Failed to retrieve latest update history record.", ex);
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
                cmd.CommandText = "DELETE FROM UpdateHistory WHERE InstallationTime < $before;";
                cmd.Parameters.AddWithValue("$before", beforeUtc.ToString("O"));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new HistoryPersistenceException($"Failed to clean up update history records before {beforeUtc}.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        private static UpdateHistoryRecord ReadRecord(SqliteDataReader reader)
        {
            return new UpdateHistoryRecord
            {
                Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
                PackageId = Guid.Parse(reader.GetString(reader.GetOrdinal("PackageId"))),
                Version = reader.GetString(reader.GetOrdinal("Version")),
                PreviousVersion = reader.GetString(reader.GetOrdinal("PreviousVersion")),
                InstallationTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("InstallationTime"))),
                CompletionTime = reader.IsDBNull(reader.GetOrdinal("CompletionTime"))
                    ? null
                    : DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletionTime"))),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Duration = TimeSpan.FromSeconds(reader.GetInt32(reader.GetOrdinal("DurationSeconds"))),
                ErrorCode = reader.IsDBNull(reader.GetOrdinal("ErrorCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("ErrorCode")),
                Result = reader.IsDBNull(reader.GetOrdinal("Result")) ? string.Empty : reader.GetString(reader.GetOrdinal("Result")),
                DeviceIdentifier = reader.GetString(reader.GetOrdinal("DeviceIdentifier")),
                TelemetryUploaded = reader.GetInt32(reader.GetOrdinal("TelemetryUploaded")) == 1
            };
        }
    }
}
