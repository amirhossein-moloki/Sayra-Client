using System;
using System.IO;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Security.Crypto;

namespace SayraClient.RemoteOperations.Services
{
    public class LocalDatabaseService : ILocalDatabaseService
    {
        private readonly ILogger<LocalDatabaseService> _logger;
        private readonly IDatabaseMigrationService _migrationService;
        private readonly ICryptographyService? _cryptographyService;
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isInitialized;
        private bool _isDisposed;

        public LocalDatabaseService(
            ILogger<LocalDatabaseService> logger,
            IDatabaseMigrationService migrationService,
            ICryptographyService? cryptographyService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));
            _cryptographyService = cryptographyService;

            string dbDir;
            string envPath = Environment.GetEnvironmentVariable("SAYRA_TEST_DB_PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                _dbPath = envPath;
                dbDir = Path.GetDirectoryName(_dbPath)!;
            }
            else
            {
                if (OperatingSystem.IsWindows())
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    dbDir = Path.Combine(appData, "Sayra", "SecureStorage");
                }
                else
                {
                    dbDir = Path.Combine(AppContext.BaseDirectory, "Data", "SecureStorage");
                }
                _dbPath = Path.Combine(dbDir, "remote_commands.db");
            }

            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }

            // Retrieve the key from DatabaseKeyManager (DPAPI secured on Windows)
            string password = DatabaseKeyManager.GetOrInitializeKey(_cryptographyService);

            var connBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Cache = SqliteCacheMode.Shared,
                Password = password
            };

            _connectionString = connBuilder.ConnectionString;
        }

        public string GetConnectionString() => _connectionString;

        public Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            var conn = new SqliteConnection(_connectionString);
            return Task.FromResult<DbConnection>(conn);
        }

        public DbConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_isInitialized) return;

                _logger.LogInformation("Initializing local secure command database at '{Path}'...", _dbPath);

                try
                {
                    using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);

                    // Enable WAL mode for high performance and concurrency
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA journal_mode=WAL;";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // Apply migrations
                    await _migrationService.ApplyMigrationsAsync(connection, cancellationToken);

                    _isInitialized = true;
                    _logger.LogInformation("Local secure database initialized successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize encrypted database. Attempting recovery...");
                    await HandleDatabaseCorruptionAsync(cancellationToken);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_dbPath)) return true;

                try
                {
                    using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);

                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "PRAGMA integrity_check;";
                    var result = await cmd.ExecuteScalarAsync(cancellationToken) as string;

                    bool isHealthy = string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
                    if (!isHealthy)
                    {
                        _logger.LogCritical("Encrypted database integrity check failed! Result: {Result}", result);
                    }
                    return isHealthy;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error verifying database integrity.");
                    return false;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task CloseSafelyAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _logger.LogInformation("Closing local database connections safely.");
                SqliteConnection.ClearAllPools();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task HandleDatabaseCorruptionAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("CRITICAL: Handling corrupted/malformed database. Attempting to backup and recreate...");

            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (File.Exists(_dbPath))
            {
                string backupPath = $"{_dbPath}.corrupted.{DateTime.UtcNow:yyyyMMddHHmmss}";
                try
                {
                    File.Move(_dbPath, backupPath, overwrite: true);
                    _logger.LogWarning("Corrupted database file moved to '{Path}' for forensic analysis.", backupPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move corrupted database. Deleting the file.");
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

            // Retry initialization
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await _migrationService.ApplyMigrationsAsync(connection, cancellationToken);
            _isInitialized = true;
            _logger.LogInformation("Encrypted database successfully recreated and initialized.");
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _lock.Dispose();
        }
    }
}
