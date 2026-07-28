using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;
using Sayra.Client.Shared.Security.Crypto;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Thread-safe enterprise health monitor that validates SQLCipher integrity and schema validity.
    /// </summary>
    public class DatabaseHealthMonitor : IDatabaseHealthMonitor
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public DatabaseHealthMonitor(
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

        public async Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var result = await cmd.ExecuteScalarAsync(cancellationToken);

                return result != null && string.Equals(result.ToString(), "ok", StringComparison.OrdinalIgnoreCase);
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

        public async Task<bool> ValidateSchemaAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                // Run a fast schema verify query across tables
                var tables = new[] { "DbVersion", "UpdateHistory", "RollbackLogs", "CacheEntries" };
                foreach (var table in tables)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
                    cmd.Parameters.AddWithValue("$name", table);

                    var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
                    if (count == 0)
                    {
                        return false;
                    }
                }

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
    }
}
