using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.RemoteOperations.Services
{
    public class DatabaseMigrationService : IDatabaseMigrationService
    {
        private readonly ILogger<DatabaseMigrationService> _logger;

        public DatabaseMigrationService(ILogger<DatabaseMigrationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ApplyMigrationsAsync(DbConnection connection, CancellationToken cancellationToken = default)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            _logger.LogInformation("Checking and applying database migrations...");

            // Create migration version table if it doesn't exist
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

            _logger.LogInformation("Current database schema version: {CurrentVersion}", currentVersion);

            // Migration 1: Initial schema creation
            if (currentVersion < 1)
            {
                using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    _logger.LogInformation("Applying migration version 1: Initial database schema.");

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS RemoteCommandHistory (
                                CommandId TEXT PRIMARY KEY NOT NULL,
                                Action TEXT NOT NULL,
                                TargetPcId TEXT NOT NULL,
                                SenderAdminId TEXT NOT NULL,
                                PayloadJson TEXT,
                                Status TEXT NOT NULL,
                                ErrorMessage TEXT,
                                ReceivedAt TEXT NOT NULL,
                                StartedAt TEXT,
                                CompletedAt TEXT,
                                ExecutionDurationMs INTEGER,
                                Signature TEXT NOT NULL,
                                RetryCount INTEGER NOT NULL DEFAULT 0
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_RemoteCommandHistory_Status_ReceivedAt ON RemoteCommandHistory (Status, ReceivedAt);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_RemoteCommandHistory_TargetPcId ON RemoteCommandHistory (TargetPcId);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_RemoteCommandHistory_SenderAdminId ON RemoteCommandHistory (SenderAdminId);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS DeadLetterCommand (
                                CommandId TEXT PRIMARY KEY NOT NULL,
                                OriginalAction TEXT NOT NULL,
                                FailureReason TEXT NOT NULL,
                                RetryCount INTEGER NOT NULL,
                                CreatedAt TEXT NOT NULL,
                                MovedAt TEXT NOT NULL
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS AuditEntry (
                                AuditId TEXT PRIMARY KEY NOT NULL,
                                CorrelationId TEXT NOT NULL,
                                EventType TEXT NOT NULL,
                                CommandId TEXT NOT NULL,
                                Timestamp TEXT NOT NULL,
                                Details TEXT NOT NULL,
                                PreviousHash TEXT NOT NULL,
                                CurrentHash TEXT NOT NULL
                            );";
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

            // Migration 2: AppliedPolicies table creation
            if (currentVersion < 2)
            {
                using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    _logger.LogInformation("Applying migration version 2: AppliedPolicies table schema.");

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS AppliedPolicies (
                                PolicyId TEXT PRIMARY KEY NOT NULL,
                                Category TEXT NOT NULL,
                                RulesJson TEXT NOT NULL,
                                VersionCode INTEGER NOT NULL,
                                LastUpdatedAt TEXT NOT NULL,
                                IsActive INTEGER DEFAULT 1,
                                Signature TEXT NOT NULL
                            );";
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

            // Migration 3: Fleet Management, Machine Groups, Bulk Operations, Alerts, Dynamic Collections
            if (currentVersion < 3)
            {
                using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    _logger.LogInformation("Applying migration version 3: Enterprise Fleet Management schema.");

                    // 1. Workstations
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Workstations (
                                MachineId TEXT PRIMARY KEY NOT NULL,
                                Status TEXT NOT NULL,
                                MetadataJson TEXT NOT NULL,
                                RegisteredAt TEXT NOT NULL,
                                LastSeenAt TEXT NOT NULL
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // 2. MachineGroups
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS MachineGroups (
                                GroupId TEXT PRIMARY KEY NOT NULL,
                                Name TEXT NOT NULL,
                                Description TEXT NOT NULL,
                                IsDynamic INTEGER NOT NULL,
                                CreatedAt TEXT NOT NULL,
                                ParentGroupId TEXT
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // 3. MachineAssignments
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS MachineAssignments (
                                AssignmentId TEXT PRIMARY KEY NOT NULL,
                                MachineId TEXT NOT NULL,
                                GroupId TEXT NOT NULL,
                                AssignedAt TEXT NOT NULL
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_MachineAssignments_MachineId ON MachineAssignments (MachineId);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_MachineAssignments_GroupId ON MachineAssignments (GroupId);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // 4. BulkOperations
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS BulkOperations (
                                OperationId TEXT PRIMARY KEY NOT NULL,
                                Action TEXT NOT NULL,
                                TargetType TEXT NOT NULL,
                                TargetValue TEXT NOT NULL,
                                Payload TEXT NOT NULL,
                                Status TEXT NOT NULL,
                                RetryCount INTEGER NOT NULL,
                                MaxRetries INTEGER NOT NULL,
                                StartedAt TEXT NOT NULL,
                                CompletedAt TEXT
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // 5. BulkOperationResults
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS BulkOperationResults (
                                ResultId TEXT PRIMARY KEY NOT NULL,
                                OperationId TEXT NOT NULL,
                                MachineId TEXT NOT NULL,
                                Success INTEGER NOT NULL,
                                ErrorMessage TEXT NOT NULL,
                                RetryCount INTEGER NOT NULL,
                                Status TEXT NOT NULL,
                                CompletedAt TEXT
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_BulkOpResults_OperationId ON BulkOperationResults (OperationId);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_BulkOpResults_MachineId ON BulkOperationResults (MachineId);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // 6. FleetAlerts
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS FleetAlerts (
                                AlertId TEXT PRIMARY KEY NOT NULL,
                                MachineId TEXT NOT NULL,
                                RuleId TEXT NOT NULL,
                                MetricName TEXT NOT NULL,
                                Value TEXT NOT NULL,
                                Threshold TEXT NOT NULL,
                                Severity TEXT NOT NULL,
                                CooldownSeconds INTEGER NOT NULL,
                                TriggeredAt TEXT NOT NULL,
                                Status TEXT NOT NULL,
                                ResolvedAt TEXT,
                                EscalationLevel INTEGER NOT NULL
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_FleetAlerts_MachineId ON FleetAlerts (MachineId);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_FleetAlerts_Status ON FleetAlerts (Status);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // 7. DynamicCollections
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS DynamicCollections (
                                CollectionId TEXT PRIMARY KEY NOT NULL,
                                Name TEXT NOT NULL,
                                RuleJson TEXT NOT NULL,
                                CreatedAt TEXT NOT NULL
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // 8. CollectionMembership
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS CollectionMembership (
                                MembershipId TEXT PRIMARY KEY NOT NULL,
                                MachineId TEXT NOT NULL,
                                CollectionId TEXT NOT NULL,
                                JoinedAt TEXT NOT NULL
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_CollMembership_MachineId ON CollectionMembership (MachineId);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_CollMembership_CollectionId ON CollectionMembership (CollectionId);";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // 9. AlertRules (to persist custom Rules!)
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS AlertRules (
                                RuleId TEXT PRIMARY KEY NOT NULL,
                                MetricName TEXT NOT NULL,
                                Operator TEXT NOT NULL,
                                Threshold TEXT NOT NULL,
                                Severity TEXT NOT NULL,
                                CooldownSeconds INTEGER NOT NULL,
                                EscalationTimeoutSeconds INTEGER NOT NULL,
                                AutoResolve INTEGER NOT NULL,
                                EscalationPath TEXT NOT NULL
                            );";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // Update version code
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

            _logger.LogInformation("Database migrations completed successfully.");
        }
    }
}
