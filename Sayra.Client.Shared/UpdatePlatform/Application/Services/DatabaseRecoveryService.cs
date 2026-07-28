using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Thread-safe database recovery engine responsible for safely resetting corrupt update platform databases.
    /// </summary>
    public class DatabaseRecoveryService : IDatabaseRecoveryService
    {
        private readonly IDatabaseMigrationService _migrationService;
        private readonly string _dbPath;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public DatabaseRecoveryService(
            IDatabaseMigrationService migrationService,
            IOptions<StorageOptions> storageOptions)
        {
            _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));

            var options = storageOptions.Value;
            if (string.IsNullOrEmpty(options.DatabasePath))
            {
                _dbPath = Path.Combine(AppContext.BaseDirectory, "Data", "update_platform.db");
            }
            else
            {
                _dbPath = options.DatabasePath;
            }
        }

        public async Task<bool> RecreateDatabaseAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                // 1. Force clear all SQLite connections and release background locks
                SqliteConnection.ClearAllPools();

                // 2. Aggressively delete the main database file and its WAL logs
                DeleteFileWithRetries(_dbPath);
                DeleteFileWithRetries(_dbPath + "-wal");
                DeleteFileWithRetries(_dbPath + "-shm");

                // 3. Re-run schema migrations to bring a healthy, empty database online
                await _migrationService.MigrateAsync(cancellationToken);

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> RecoverAndRecreateAsync(Exception ex, CancellationToken cancellationToken = default)
        {
            // Abstractions can log the incoming exception here if telemetry/logging is integrated
            return await RecreateDatabaseAsync(cancellationToken);
        }

        private static void DeleteFileWithRetries(string filePath)
        {
            if (!File.Exists(filePath)) return;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    File.Delete(filePath);
                    return;
                }
                catch
                {
                    Thread.Sleep(50 * (i + 1));
                }
            }
        }
    }
}
