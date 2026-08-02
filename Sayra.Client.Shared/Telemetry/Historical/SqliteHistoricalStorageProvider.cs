using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Exceptions;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Security.Crypto;

namespace Sayra.Client.Shared.Telemetry.Historical
{
    /// <summary>
    /// SQLCipher-encrypted SQLite implementation of the Historical Storage Provider.
    /// Uses serialized single-writer locking to completely avoid SQLite contention.
    /// </summary>
    public class SqliteHistoricalStorageProvider : IHistoricalStorageProvider
    {
        private readonly HistoricalStorageOptions _options;
        private readonly ICryptographyService? _cryptographyService;
        private readonly ILogger<SqliteHistoricalStorageProvider> _logger;
        private readonly string _connectionString;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly string _dbPath;

        public string ProviderName => "SQLite (SQLCipher)";

        public SqliteHistoricalStorageProvider(
            IOptions<HistoricalStorageOptions> options,
            ILogger<SqliteHistoricalStorageProvider> logger,
            ICryptographyService? cryptographyService = null)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cryptographyService = cryptographyService;

            if (string.IsNullOrWhiteSpace(_options.DatabasePath))
            {
                _dbPath = Path.Combine(AppContext.BaseDirectory, "Data", "historical_metrics.db");
            }
            else
            {
                _dbPath = _options.DatabasePath;
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

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Initializing SQLCipher historical storage provider at: {DbPath}", _dbPath);

            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                // Apply SQLCipher page size configuration if requested
                if (_options.PageSize > 0)
                {
                    using var pragmaCmd = connection.CreateCommand();
                    pragmaCmd.CommandText = $"PRAGMA page_size = {_options.PageSize};";
                    await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Enable WAL mode for high concurrency
                using var walCmd = connection.CreateCommand();
                walCmd.CommandText = "PRAGMA journal_mode = WAL;";
                await walCmd.ExecuteNonQueryAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();

                // 1. HistoricalMetrics Table
                using var cmd1 = connection.CreateCommand();
                cmd1.Transaction = transaction;
                cmd1.CommandText = @"
                    CREATE TABLE IF NOT EXISTS HistoricalMetrics (
                        Timestamp TEXT NOT NULL,
                        MetricName TEXT NOT NULL,
                        Category INTEGER NOT NULL,
                        Unit INTEGER NOT NULL,
                        AverageValue REAL NOT NULL,
                        MinValue REAL NOT NULL,
                        MaxValue REAL NOT NULL,
                        Count INTEGER NOT NULL,
                        Interval INTEGER NOT NULL,
                        PRIMARY KEY (Timestamp, MetricName, Interval)
                    );";
                await cmd1.ExecuteNonQueryAsync(cancellationToken);

                // Create index on HistoricalMetrics for range queries
                using var idxCmd1 = connection.CreateCommand();
                idxCmd1.Transaction = transaction;
                idxCmd1.CommandText = "CREATE INDEX IF NOT EXISTS IDX_HistoricalMetrics_Query ON HistoricalMetrics (MetricName, Timestamp, Interval);";
                await idxCmd1.ExecuteNonQueryAsync(cancellationToken);

                // 2. MetricSeries Table
                using var cmd2 = connection.CreateCommand();
                cmd2.Transaction = transaction;
                cmd2.CommandText = @"
                    CREATE TABLE IF NOT EXISTS MetricSeries (
                        MetricName TEXT PRIMARY KEY,
                        Category INTEGER NOT NULL,
                        Unit INTEGER NOT NULL,
                        Points BLOB NOT NULL
                    );";
                await cmd2.ExecuteNonQueryAsync(cancellationToken);

                // 3. PerformanceSnapshots Table
                using var cmd3 = connection.CreateCommand();
                cmd3.Transaction = transaction;
                cmd3.CommandText = @"
                    CREATE TABLE IF NOT EXISTS PerformanceSnapshots (
                        Timestamp TEXT NOT NULL,
                        StartupTimeMs INTEGER NOT NULL,
                        AuthenticationTimeMs INTEGER NOT NULL,
                        DatabaseLatencyMs INTEGER NOT NULL,
                        IpcLatencyMs INTEGER NOT NULL,
                        TcpLatencyMs INTEGER NOT NULL,
                        DownloadSpeed REAL NOT NULL,
                        UploadSpeed REAL NOT NULL,
                        DiskLatencyMs INTEGER NOT NULL,
                        CacheHitRatio REAL NOT NULL,
                        QueueLength INTEGER NOT NULL,
                        WorkerExecutionTimeMs INTEGER NOT NULL,
                        GarbageCollectionCount INTEGER NOT NULL,
                        ThreadPoolThreads INTEGER NOT NULL,
                        AsyncOperationsCount INTEGER NOT NULL,
                        MachineId TEXT NOT NULL,
                        Subsystem TEXT,
                        Operation TEXT,
                        Status TEXT,
                        TraceId TEXT,
                        CorrelationId TEXT,
                        DurationMs INTEGER NOT NULL
                    );";
                await cmd3.ExecuteNonQueryAsync(cancellationToken);

                // Index on PerformanceSnapshots
                using var idxCmd3 = connection.CreateCommand();
                idxCmd3.Transaction = transaction;
                idxCmd3.CommandText = "CREATE INDEX IF NOT EXISTS IDX_PerformanceSnapshots_Query ON PerformanceSnapshots (Timestamp, Subsystem, CorrelationId);";
                await idxCmd3.ExecuteNonQueryAsync(cancellationToken);

                // 4. AuditMetrics Table
                using var cmd4 = connection.CreateCommand();
                cmd4.Transaction = transaction;
                cmd4.CommandText = @"
                    CREATE TABLE IF NOT EXISTS AuditMetrics (
                        AuditId TEXT PRIMARY KEY,
                        Timestamp TEXT NOT NULL,
                        Name TEXT NOT NULL,
                        MachineId TEXT NOT NULL,
                        SessionId TEXT,
                        UserId TEXT,
                        OperatorId TEXT,
                        Details TEXT NOT NULL,
                        Count INTEGER NOT NULL,
                        DurationMs INTEGER NOT NULL
                    );";
                await cmd4.ExecuteNonQueryAsync(cancellationToken);

                // Index on AuditMetrics
                using var idxCmd4 = connection.CreateCommand();
                idxCmd4.Transaction = transaction;
                idxCmd4.CommandText = "CREATE INDEX IF NOT EXISTS IDX_AuditMetrics_Query ON AuditMetrics (Timestamp, Name, SessionId);";
                await idxCmd4.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("SQLCipher historical storage initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SQLCipher historical storage.");
                throw new HistoricalStorageException("Failed to initialize database schema.", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task ExecuteNonQueryAsync(string sql, Dictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecuteNonQuery failed for SQL: {Sql}", sql);
                throw new HistoricalStorageException($"Database execute failed.", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<List<T>> QueryAsync<T>(string sql, Dictionary<string, object?> parameters, Func<IDataRecord, T> map, CancellationToken cancellationToken = default)
        {
            // Reads do not need the _writeLock since SQLite allows concurrent reading, especially under WAL mode!
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }

                var list = new List<T>();
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(map(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Query failed for SQL: {Sql}", sql);
                throw new HistoricalStorageException($"Database query failed.", ex);
            }
        }

        public async Task ExecuteBatchAsync(string sql, IEnumerable<Dictionary<string, object?>> batchParameters, CancellationToken cancellationToken = default)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();

                foreach (var parameters in batchParameters)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = sql;
                    foreach (var param in parameters)
                    {
                        cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecuteBatch failed for SQL: {Sql}", sql);
                throw new HistoricalStorageException($"Database batch transaction failed.", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public long GetStorageSizeBytes()
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
                _logger.LogWarning(ex, "Failed to retrieve storage file size for: {DbPath}", _dbPath);
            }
            return 0;
        }
    }
}
