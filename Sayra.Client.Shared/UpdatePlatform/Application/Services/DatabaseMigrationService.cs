using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;
using Sayra.Client.Shared.Security.Crypto;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Production-ready service that handles localized schema migrations for the SQLCipher update database.
    /// </summary>
    public class DatabaseMigrationService : IDatabaseMigrationService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly ICryptographyService? _cryptographyService;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public DatabaseMigrationService(
            IOptions<StorageOptions> storageOptions,
            ICryptographyService? cryptographyService = null)
        {
            _cryptographyService = cryptographyService;

            var options = storageOptions.Value;
            if (string.IsNullOrEmpty(options.DatabasePath))
            {
                var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }
                _dbPath = Path.Combine(dataDir, "update_platform.db");
            }
            else
            {
                _dbPath = options.DatabasePath;
                var dir = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            var connBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Cache = SqliteCacheMode.Private,
                Password = DatabaseKeyManager.GetOrInitializeKey(_cryptographyService),
                Pooling = false
            };
            _connectionString = connBuilder.ConnectionString;
        }

        public string ConnectionString => _connectionString;

        public async Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    using var connection = new SqliteConnection(_connectionString);
                    await connection.OpenAsync(cancellationToken);

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    int currentVersion = await ReadCurrentVersionAsync(connection, cancellationToken);
                    if (currentVersion < 1)
                    {
                        using var transaction = connection.BeginTransaction();
                        try
                        {
                            // Version 1 Migration: Initial Setup of Update Schema
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandText = @"
                                    CREATE TABLE IF NOT EXISTS DbVersion (
                                        Version INTEGER PRIMARY KEY,
                                        MigrationName TEXT NOT NULL,
                                        AppliedAt TEXT NOT NULL
                                    );";
                                await cmd.ExecuteNonQueryAsync(cancellationToken);
                            }

                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandText = @"
                                    CREATE TABLE IF NOT EXISTS UpdateHistory (
                                        Id TEXT PRIMARY KEY NOT NULL,
                                        PackageId TEXT NOT NULL,
                                        Version TEXT NOT NULL,
                                        PreviousVersion TEXT NOT NULL,
                                        InstallationTime TEXT NOT NULL,
                                        CompletionTime TEXT,
                                        Status TEXT NOT NULL,
                                        DurationSeconds INTEGER DEFAULT 0,
                                        ErrorCode TEXT,
                                        Result TEXT,
                                        DeviceIdentifier TEXT NOT NULL,
                                        TelemetryUploaded INTEGER DEFAULT 0
                                    );";
                                await cmd.ExecuteNonQueryAsync(cancellationToken);
                            }

                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_UpdateHistory_Status ON UpdateHistory(Status);";
                                await cmd.ExecuteNonQueryAsync(cancellationToken);
                            }

                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandText = @"
                                    CREATE TABLE IF NOT EXISTS RollbackLogs (
                                        Id TEXT PRIMARY KEY NOT NULL,
                                        Reason TEXT NOT NULL,
                                        TriggerSource TEXT NOT NULL,
                                        PreviousVersion TEXT NOT NULL,
                                        RestoredVersion TEXT NOT NULL,
                                        DurationSeconds INTEGER DEFAULT 0,
                                        Result TEXT NOT NULL,
                                        FailureDetails TEXT,
                                        Timestamp TEXT NOT NULL
                                    );";
                                await cmd.ExecuteNonQueryAsync(cancellationToken);
                            }

                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandText = @"
                                    CREATE TABLE IF NOT EXISTS CacheEntries (
                                        Key TEXT PRIMARY KEY NOT NULL,
                                        FilePath TEXT NOT NULL,
                                        EntryType TEXT NOT NULL,
                                        Version TEXT NOT NULL,
                                        SizeBytes INTEGER NOT NULL,
                                        Sha256Hash TEXT NOT NULL,
                                        CreatedAt TEXT NOT NULL,
                                        LastAccessedAt TEXT NOT NULL,
                                        ExpiresAt TEXT,
                                        IsLocked INTEGER DEFAULT 0,
                                        IsValid INTEGER DEFAULT 1
                                    );";
                                await cmd.ExecuteNonQueryAsync(cancellationToken);
                            }

                            // Insert Schema Version
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandText = "INSERT INTO DbVersion (Version, MigrationName, AppliedAt) VALUES ($v, $name, $applied);";
                                cmd.Parameters.AddWithValue("$v", 1);
                                cmd.Parameters.AddWithValue("$name", "InitialSetup");
                                cmd.Parameters.AddWithValue("$applied", DateTime.UtcNow.ToString("O"));
                                await cmd.ExecuteNonQueryAsync(cancellationToken);
                            }

                            await transaction.CommitAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            throw new DatabaseMigrationException("Failed to apply initial schema migration Version 1.", ex);
                        }
                    }
                }, cancellationToken);
            }
            catch (Exception ex) when (!(ex is DatabaseMigrationException))
            {
                throw new DatabaseMigrationException("Failed to complete database migration processes.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                return await ReadCurrentVersionAsync(connection, cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<int> ReadCurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT MAX(Version) FROM DbVersion;";
                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            catch
            {
                // DbVersion table doesn't exist yet
                return 0;
            }
        }

        private async Task ExecuteWithRetryAsync(Func<Task> action, CancellationToken cancellationToken)
        {
            int maxAttempts = 3;
            int delayMs = 50;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await action();
                    return;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
                {
                    if (attempt == maxAttempts)
                    {
                        throw;
                    }
                    await Task.Delay(delayMs * attempt, cancellationToken);
                }
            }
        }
    }
}
