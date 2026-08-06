using System;
using System.IO;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Security.Crypto;

namespace Sayra.Client.Shared.Fleet.Infrastructure
{
    /// <summary>
    /// Production-grade SQLCipher implementation of the fleet database context.
    /// </summary>
    public class FleetDatabaseContext : IFleetDatabaseContext
    {
        private readonly ILogger<FleetDatabaseContext> _logger;
        private readonly ICryptographyService? _cryptographyService;
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isInitialized;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FleetDatabaseContext"/> class.
        /// </summary>
        public FleetDatabaseContext(
            ILogger<FleetDatabaseContext> logger,
            ICryptographyService? cryptographyService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cryptographyService = cryptographyService;

            string dbDir;
            string envPath = Environment.GetEnvironmentVariable("SAYRA_TEST_DB_PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                // Put the fleet database in the same directory as the test command database
                dbDir = Path.GetDirectoryName(envPath)!;
                _dbPath = Path.Combine(dbDir, "fleet_management.db");
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
                _dbPath = Path.Combine(dbDir, "fleet_management.db");
            }

            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }

            // Retrieve encrypted SQLCipher master key (DPAPI secured on Windows)
            string password = DatabaseKeyManager.GetOrInitializeKey(_cryptographyService);

            var connBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Cache = SqliteCacheMode.Shared,
                Password = password
            };

            _connectionString = connBuilder.ConnectionString;
        }

        /// <inheritdoc />
        public string GetConnectionString() => _connectionString;

        /// <inheritdoc />
        public DbConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        /// <inheritdoc />
        public Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            var conn = new SqliteConnection(_connectionString);
            return Task.FromResult<DbConnection>(conn);
        }

        /// <inheritdoc />
        public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_isInitialized) return;

                _logger.LogInformation("Initializing local secure fleet database at '{Path}'...", _dbPath);

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

                    // Apply schema migrations
                    await ApplyMigrationsInternalAsync(connection, cancellationToken);

                    _isInitialized = true;
                    _logger.LogInformation("Local secure fleet database initialized successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize encrypted fleet database. Attempting recovery...");
                    await HandleDatabaseCorruptionAsync(cancellationToken);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
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
                        _logger.LogCritical("Encrypted fleet database integrity check failed! Result: {Result}", result);
                    }
                    return isHealthy;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error verifying fleet database integrity.");
                    return false;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task CloseSafelyAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _logger.LogInformation("Closing secure fleet database connections safely.");
                SqliteConnection.ClearAllPools();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task ApplyMigrationsInternalAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            // Create migration version table
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS SchemaVersion (
                        Version INTEGER PRIMARY KEY,
                        AppliedAt TEXT NOT NULL
                    );";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            int currentVersion = 0;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT MAX(Version) FROM SchemaVersion;";
                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                if (result != null && result != DBNull.Value)
                {
                    currentVersion = Convert.ToInt32(result);
                }
            }

            _logger.LogInformation("Current fleet database schema version: {CurrentVersion}", currentVersion);

            if (currentVersion < 1)
            {
                using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    _logger.LogInformation("Applying migration version 1: Fleet Management Core Schema.");

                    string[] tables = new string[]
                    {
                        @"CREATE TABLE IF NOT EXISTS Workstations (
                            MachineId TEXT PRIMARY KEY NOT NULL,
                            Hostname TEXT NOT NULL,
                            IpAddress TEXT NOT NULL,
                            MacAddress TEXT NOT NULL,
                            Status TEXT NOT NULL,
                            HealthStatus TEXT NOT NULL,
                            LastSeenUtc TEXT NOT NULL,
                            SemVer TEXT NOT NULL,
                            BuildHash TEXT NOT NULL,
                            BuildDate TEXT NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS Groups (
                            GroupId TEXT PRIMARY KEY NOT NULL,
                            Name TEXT NOT NULL,
                            Description TEXT NOT NULL,
                            GroupType TEXT NOT NULL,
                            DynamicRuleExpression TEXT NOT NULL,
                            ParentGroupId TEXT
                        );",

                        @"CREATE TABLE IF NOT EXISTS GroupMembership (
                            GroupId TEXT NOT NULL,
                            MachineId TEXT NOT NULL,
                            PRIMARY KEY (GroupId, MachineId)
                        );",

                        @"CREATE TABLE IF NOT EXISTS Regions (
                            RegionId TEXT PRIMARY KEY NOT NULL,
                            Name TEXT NOT NULL,
                            RegionType TEXT NOT NULL,
                            ParentRegionId TEXT
                        );",

                        @"CREATE TABLE IF NOT EXISTS Departments (
                            DepartmentId TEXT PRIMARY KEY NOT NULL,
                            Name TEXT NOT NULL,
                            DepartmentType TEXT NOT NULL,
                            ParentDepartmentId TEXT
                        );",

                        @"CREATE TABLE IF NOT EXISTS Tags (
                            Key TEXT NOT NULL,
                            Value TEXT NOT NULL,
                            MachineId TEXT NOT NULL,
                            PRIMARY KEY (Key, Value, MachineId)
                        );",

                        @"CREATE TABLE IF NOT EXISTS Snapshots (
                            MachineId TEXT PRIMARY KEY NOT NULL,
                            CapturedAt TEXT NOT NULL,
                            Connection TEXT NOT NULL,
                            Compliance TEXT NOT NULL,
                            ActiveSessionId TEXT NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS Health (
                            MachineId TEXT PRIMARY KEY NOT NULL,
                            OverallHealthScore REAL NOT NULL,
                            ActiveWarningsCount INTEGER NOT NULL,
                            ActiveEmergenciesCount INTEGER NOT NULL,
                            SubsystemScoresJson TEXT NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS HealthHistory (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            MachineId TEXT NOT NULL,
                            TimestampUtc TEXT NOT NULL,
                            CpuUtilization REAL NOT NULL,
                            MemoryUtilization REAL NOT NULL,
                            StorageUtilization REAL NOT NULL,
                            NetworkThroughput REAL NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS Inventory (
                            MachineId TEXT PRIMARY KEY NOT NULL,
                            CpuName TEXT NOT NULL,
                            GpuName TEXT NOT NULL,
                            RamGb INTEGER NOT NULL,
                            OperatingSystem TEXT NOT NULL,
                            StorageDrivesJson TEXT NOT NULL
                        );"
                    };

                    foreach (var tableSql in tables)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = tableSql;
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // Indices for high-performance query/filtering
                    string[] indices = new string[]
                    {
                        "CREATE INDEX IF NOT EXISTS IDX_GroupMembership_MachineId ON GroupMembership (MachineId);",
                        "CREATE INDEX IF NOT EXISTS IDX_GroupMembership_GroupId ON GroupMembership (GroupId);",
                        "CREATE INDEX IF NOT EXISTS IDX_Tags_MachineId ON Tags (MachineId);",
                        "CREATE INDEX IF NOT EXISTS IDX_HealthHistory_MachineId ON HealthHistory (MachineId);",
                        "CREATE INDEX IF NOT EXISTS IDX_Workstations_Status ON Workstations (Status);"
                    };

                    foreach (var indexSql in indices)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = indexSql;
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "INSERT INTO SchemaVersion (Version, AppliedAt) VALUES (1, $appliedAt);";
                        var parameter = cmd.CreateParameter();
                        parameter.ParameterName = "$appliedAt";
                        parameter.Value = DateTime.UtcNow.ToString("O");
                        cmd.Parameters.Add(parameter);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogInformation("Migration version 1 applied successfully.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Failed to apply migration version 1. Transaction rolled back.");
                    throw;
                }
            }

            if (currentVersion < 2)
            {
                using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    _logger.LogInformation("Applying migration version 2: Asset Management & Maintenance Core Schema.");

                    string[] tables = new string[]
                    {
                        @"CREATE TABLE IF NOT EXISTS Assets (
                            AssetId TEXT PRIMARY KEY NOT NULL,
                            MachineId TEXT NOT NULL,
                            Name TEXT NOT NULL,
                            SerialOrSignature TEXT NOT NULL,
                            Category TEXT NOT NULL,
                            Status TEXT NOT NULL,
                            SpecificationsJson TEXT NOT NULL,
                            Manufacturer TEXT NOT NULL,
                            Version TEXT NOT NULL,
                            DriverVersion TEXT NOT NULL,
                            SoftwareName TEXT NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS AssetHistory (
                            HistoryId TEXT PRIMARY KEY NOT NULL,
                            AssetId TEXT NOT NULL,
                            MachineId TEXT NOT NULL,
                            TimestampUtc TEXT NOT NULL,
                            EventType TEXT NOT NULL,
                            Description TEXT NOT NULL,
                            OperatorId TEXT NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS AssetChanges (
                            ChangeId TEXT PRIMARY KEY NOT NULL,
                            AssetId TEXT NOT NULL,
                            MachineId TEXT NOT NULL,
                            TimestampUtc TEXT NOT NULL,
                            ChangeType TEXT NOT NULL,
                            PropertyName TEXT NOT NULL,
                            OldValue TEXT NOT NULL,
                            NewValue TEXT NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS MaintenanceSchedules (
                            ScheduleId TEXT PRIMARY KEY NOT NULL,
                            WindowId TEXT NOT NULL,
                            Category TEXT NOT NULL,
                            StartTimeUtc TEXT NOT NULL,
                            DurationMs INTEGER NOT NULL,
                            ForceSessionTermination INTEGER NOT NULL,
                            ScopeFilter TEXT NOT NULL,
                            State TEXT NOT NULL,
                            ExecutionSummary TEXT NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS MaintenanceExecutions (
                            ExecutionId TEXT PRIMARY KEY NOT NULL,
                            ScheduleId TEXT NOT NULL,
                            MachineId TEXT NOT NULL,
                            Status TEXT NOT NULL,
                            StartTimeUtc TEXT,
                            EndTimeUtc TEXT,
                            OutputLogs TEXT NOT NULL,
                            ErrorMessage TEXT NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS MaintenanceHistory (
                            HistoryId TEXT PRIMARY KEY NOT NULL,
                            ScheduleId TEXT NOT NULL,
                            OutcomeStatus TEXT NOT NULL,
                            AffectedMachinesJson TEXT NOT NULL,
                            StartTimeUtc TEXT NOT NULL,
                            EndTimeUtc TEXT NOT NULL,
                            Summary TEXT NOT NULL
                        );"
                    };

                    foreach (var tableSql in tables)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = tableSql;
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    string[] indices = new string[]
                    {
                        "CREATE INDEX IF NOT EXISTS IDX_Assets_MachineId ON Assets (MachineId);",
                        "CREATE INDEX IF NOT EXISTS IDX_Assets_Category ON Assets (Category);",
                        "CREATE INDEX IF NOT EXISTS IDX_AssetHistory_AssetId ON AssetHistory (AssetId);",
                        "CREATE INDEX IF NOT EXISTS IDX_AssetChanges_AssetId ON AssetChanges (AssetId);",
                        "CREATE INDEX IF NOT EXISTS IDX_MaintenanceExecutions_ScheduleId ON MaintenanceExecutions (ScheduleId);",
                        "CREATE INDEX IF NOT EXISTS IDX_MaintenanceExecutions_MachineId ON MaintenanceExecutions (MachineId);",
                        "CREATE INDEX IF NOT EXISTS IDX_MaintenanceHistory_ScheduleId ON MaintenanceHistory (ScheduleId);"
                    };

                    foreach (var indexSql in indices)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = indexSql;
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "INSERT INTO SchemaVersion (Version, AppliedAt) VALUES (2, $appliedAt);";
                        var parameter = cmd.CreateParameter();
                        parameter.ParameterName = "$appliedAt";
                        parameter.Value = DateTime.UtcNow.ToString("O");
                        cmd.Parameters.Add(parameter);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogInformation("Migration version 2 applied successfully.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Failed to apply migration version 2. Transaction rolled back.");
                    throw;
                }
            }

            if (currentVersion < 3)
            {
                using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    _logger.LogInformation("Applying migration version 3: Distributed Game Delivery & LAN Cache.");

                    string[] tables = new string[]
                    {
                        @"CREATE TABLE IF NOT EXISTS GameCacheEntries (
                            GameId TEXT PRIMARY KEY NOT NULL,
                            Version TEXT NOT NULL,
                            PackageId TEXT NOT NULL,
                            TotalBlocks INTEGER NOT NULL,
                            CompletedBlocks INTEGER NOT NULL,
                            TotalSize INTEGER NOT NULL,
                            IsHealthy INTEGER NOT NULL,
                            LastUsedUtc TEXT NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS CacheBlocks (
                            BlockId TEXT PRIMARY KEY NOT NULL,
                            Size INTEGER NOT NULL,
                            Sha256Hash TEXT NOT NULL,
                            IsStored INTEGER NOT NULL,
                            LocalPath TEXT NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS CacheNodes (
                            NodeId TEXT PRIMARY KEY NOT NULL,
                            MachineId TEXT NOT NULL,
                            Hostname TEXT NOT NULL,
                            IpAddress TEXT NOT NULL,
                            Port INTEGER NOT NULL,
                            IsOnline INTEGER NOT NULL,
                            LastSeenUtc TEXT NOT NULL,
                            FreeStorageBytes INTEGER NOT NULL,
                            IsSsd INTEGER NOT NULL,
                            NetworkSpeedMbps REAL NOT NULL,
                            CpuLoadPercent REAL NOT NULL,
                            CacheCompletenessPercent REAL NOT NULL,
                            HealthScore REAL NOT NULL
                        );",

                        @"CREATE TABLE IF NOT EXISTS BlockAvailabilities (
                            NodeId TEXT NOT NULL,
                            BlockId TEXT NOT NULL,
                            GameId TEXT NOT NULL,
                            IsAvailable INTEGER NOT NULL,
                            PRIMARY KEY (NodeId, BlockId)
                        );"
                    };

                    foreach (var tableSql in tables)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = tableSql;
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    string[] indices = new string[]
                    {
                        "CREATE INDEX IF NOT EXISTS IDX_CacheBlocks_IsStored ON CacheBlocks (IsStored);",
                        "CREATE INDEX IF NOT EXISTS IDX_BlockAvailabilities_BlockId ON BlockAvailabilities (BlockId);",
                        "CREATE INDEX IF NOT EXISTS IDX_BlockAvailabilities_NodeId ON BlockAvailabilities (NodeId);"
                    };

                    foreach (var indexSql in indices)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = indexSql;
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "INSERT INTO SchemaVersion (Version, AppliedAt) VALUES (3, $appliedAt);";
                        var parameter = cmd.CreateParameter();
                        parameter.ParameterName = "$appliedAt";
                        parameter.Value = DateTime.UtcNow.ToString("O");
                        cmd.Parameters.Add(parameter);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogInformation("Migration version 3 applied successfully.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Failed to apply migration version 3. Transaction rolled back.");
                    throw;
                }
            }
        }

        private async Task HandleDatabaseCorruptionAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("CRITICAL: Handling corrupted fleet database. Backup and recreating...");

            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (File.Exists(_dbPath))
            {
                string backupPath = $"{_dbPath}.corrupted.{DateTime.UtcNow:yyyyMMddHHmmss}";
                try
                {
                    File.Move(_dbPath, backupPath, overwrite: true);
                    _logger.LogWarning("Corrupted fleet database file moved to '{Path}'.", backupPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move corrupted database. Deleting the file.");
                    try
                    {
                        File.Delete(_dbPath);
                    }
                    catch (Exception dEx)
                    {
                        _logger.LogError(dEx, "Failed to delete corrupted database file!");
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

            await ApplyMigrationsInternalAsync(connection, cancellationToken);
            _isInitialized = true;
            _logger.LogInformation("Encrypted fleet database successfully recreated and initialized.");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _lock.Dispose();
        }
    }
}
