using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
    /// Thread-safe enterprise Cache Manager implementing update cache validation, limits, expiration, and eviction policies.
    /// </summary>
    public class CacheManager : ICacheManager
    {
        private readonly string _connectionString;
        private readonly string _cacheDirectory;
        private readonly long _maxCacheSizeBytes;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public CacheManager(
            IOptions<StorageOptions> storageOptions,
            ICryptographyService? cryptographyService = null)
        {
            var options = storageOptions.Value;

            // Resolve cache directory path
            if (string.IsNullOrEmpty(options.CacheDirectory))
            {
                _cacheDirectory = Path.Combine(AppContext.BaseDirectory, "UpdateCache");
            }
            else
            {
                _cacheDirectory = options.CacheDirectory;
            }

            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }

            _maxCacheSizeBytes = options.MaxCacheSizeMegabytes * 1024 * 1024;

            // Resolve DB path
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

        public string CacheDirectory => _cacheDirectory;

        public async Task<CacheEntry> AddOrUpdateAsync(
            string key,
            string filePath,
            string entryType,
            string version,
            long sizeBytes,
            string sha256Hash,
            DateTime? expiresAt = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("FilePath cannot be null or empty.", nameof(filePath));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                var entry = new CacheEntry
                {
                    Key = key,
                    FilePath = filePath,
                    EntryType = entryType,
                    Version = version,
                    SizeBytes = sizeBytes,
                    Sha256Hash = sha256Hash,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    IsLocked = false,
                    IsValid = true
                };

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO CacheEntries (
                        Key, FilePath, EntryType, Version, SizeBytes, Sha256Hash, CreatedAt, LastAccessedAt, ExpiresAt, IsLocked, IsValid
                    ) VALUES (
                        $key, $filePath, $entryType, $version, $sizeBytes, $sha256Hash, $createdAt, $lastAccessed, $expiresAt, $isLocked, $isValid
                    ) ON CONFLICT(Key) DO UPDATE SET
                        FilePath = excluded.FilePath,
                        EntryType = excluded.EntryType,
                        Version = excluded.Version,
                        SizeBytes = excluded.SizeBytes,
                        Sha256Hash = excluded.Sha256Hash,
                        LastAccessedAt = excluded.LastAccessedAt,
                        ExpiresAt = excluded.ExpiresAt,
                        IsValid = excluded.IsValid;";

                cmd.Parameters.AddWithValue("$key", entry.Key);
                cmd.Parameters.AddWithValue("$filePath", entry.FilePath);
                cmd.Parameters.AddWithValue("$entryType", entry.EntryType);
                cmd.Parameters.AddWithValue("$version", entry.Version);
                cmd.Parameters.AddWithValue("$sizeBytes", entry.SizeBytes);
                cmd.Parameters.AddWithValue("$sha256Hash", entry.Sha256Hash);
                cmd.Parameters.AddWithValue("$createdAt", entry.CreatedAt.ToString("O"));
                cmd.Parameters.AddWithValue("$lastAccessed", entry.LastAccessedAt.ToString("O"));
                cmd.Parameters.AddWithValue("$expiresAt", entry.ExpiresAt.HasValue ? entry.ExpiresAt.Value.ToString("O") : DBNull.Value);
                cmd.Parameters.AddWithValue("$isLocked", entry.IsLocked ? 1 : 0);
                cmd.Parameters.AddWithValue("$isValid", entry.IsValid ? 1 : 0);

                await cmd.ExecuteNonQueryAsync(cancellationToken);

                // Enforce size limits dynamically on add/update
                await EnforceSizeLimitAsync(connection, cancellationToken);

                return entry;
            }
            catch (Exception ex)
            {
                throw new StorageException($"Failed to write cache entry metadata for key '{key}'.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key)) return null;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM CacheEntries WHERE Key = $key;";
                cmd.Parameters.AddWithValue("$key", key);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var entry = ReadRecord(reader);

                    // Update last accessed time for LRU eviction policy
                    using var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = "UPDATE CacheEntries SET LastAccessedAt = $now WHERE Key = $key;";
                    updateCmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
                    updateCmd.Parameters.AddWithValue("$key", key);
                    await updateCmd.ExecuteNonQueryAsync(cancellationToken);

                    entry.LastAccessedAt = DateTime.UtcNow;
                    return entry;
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new StorageException($"Failed to retrieve cache entry with key '{key}'.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IEnumerable<CacheEntry>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                var list = new List<CacheEntry>();
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM CacheEntries ORDER BY LastAccessedAt DESC;";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(ReadRecord(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                throw new StorageException("Failed to retrieve cache entries.", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task EvictAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key)) return;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                await EvictEntryInternalAsync(connection, key, cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task EvictEntryInternalAsync(SqliteConnection connection, string key, CancellationToken cancellationToken)
        {
            string? filePath = null;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT FilePath FROM CacheEntries WHERE Key = $key;";
                cmd.Parameters.AddWithValue("$key", key);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                if (result != null)
                {
                    filePath = result.ToString();
                }
            }

            if (filePath != null)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception ex)
                {
                    throw new StorageException($"Failed to physically delete cached file at path '{filePath}'.", ex);
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM CacheEntries WHERE Key = $key;";
                cmd.Parameters.AddWithValue("$key", key);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        public async Task EvictLruAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                await EnforceSizeLimitAsync(connection, cancellationToken, forceOne: true);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task EnforceSizeLimitAsync(SqliteConnection connection, CancellationToken cancellationToken, bool forceOne = false)
        {
            while (true)
            {
                long currentTotalSize = 0;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT SUM(SizeBytes) FROM CacheEntries;";
                    var sizeRes = await cmd.ExecuteScalarAsync(cancellationToken);
                    if (sizeRes != null && sizeRes != DBNull.Value)
                    {
                        currentTotalSize = Convert.ToInt64(sizeRes);
                    }
                }

                if (!forceOne && currentTotalSize <= _maxCacheSizeBytes)
                {
                    break;
                }

                // Find LRU entry that is not locked
                string? lruKey = null;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Key FROM CacheEntries WHERE IsLocked = 0 ORDER BY LastAccessedAt ASC LIMIT 1;";
                    var keyRes = await cmd.ExecuteScalarAsync(cancellationToken);
                    if (keyRes != null)
                    {
                        lruKey = keyRes.ToString();
                    }
                }

                if (lruKey == null)
                {
                    // No unlocked cache entries found to evict! Stop to prevent infinite loops.
                    break;
                }

                await EvictEntryInternalAsync(connection, lruKey, cancellationToken);

                if (forceOne)
                {
                    break;
                }
            }
        }

        public async Task CleanExpiredAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var expiredKeys = new List<string>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Key FROM CacheEntries WHERE ExpiresAt IS NOT NULL AND ExpiresAt < $now;";
                    cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        expiredKeys.Add(reader.GetString(0));
                    }
                }

                foreach (var key in expiredKeys)
                {
                    await EvictEntryInternalAsync(connection, key, cancellationToken);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ClearInvalidAndFailedAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var invalidOrFailedKeys = new List<string>();
                using (var cmd = connection.CreateCommand())
                {
                    // Evict entries marked as invalid or entries representing failed installs/temporary files
                    cmd.CommandText = "SELECT Key, FilePath FROM CacheEntries WHERE IsValid = 0 OR EntryType = 'TemporaryDownload';";
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        invalidOrFailedKeys.Add(reader.GetString(0));
                    }
                }

                foreach (var key in invalidOrFailedKeys)
                {
                    await EvictEntryInternalAsync(connection, key, cancellationToken);
                }

                // Also scan the cache directory physically and clear any unregistered file or .tmp file
                var physicalFiles = Directory.GetFiles(_cacheDirectory);
                foreach (var file in physicalFiles)
                {
                    bool isTracked = false;
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM CacheEntries WHERE FilePath = $path;";
                        cmd.Parameters.AddWithValue("$path", file);
                        isTracked = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
                    }

                    if (!isTracked)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // Ignore lock/permission issues during aggressive cleanup sweeps
                        }
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ValidateIntegrityAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var entries = new List<CacheEntry>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM CacheEntries;";
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        entries.Add(ReadRecord(reader));
                    }
                }

                foreach (var entry in entries)
                {
                    bool isValid = true;
                    if (!File.Exists(entry.FilePath))
                    {
                        isValid = false;
                    }
                    else
                    {
                        string actualHash = await ComputeSha256Async(entry.FilePath, cancellationToken);
                        if (!string.Equals(actualHash, entry.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                        {
                            isValid = false;
                        }
                    }

                    if (!isValid)
                    {
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "UPDATE CacheEntries SET IsValid = 0 WHERE Key = $key;";
                            cmd.Parameters.AddWithValue("$key", entry.Key);
                            await cmd.ExecuteNonQueryAsync(cancellationToken);
                        }

                        throw new CacheCorruptionException($"Cache integrity validation failed for file '{entry.FilePath}'. Expected hash: {entry.Sha256Hash}");
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
        {
            using var sha = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                sha.TransformBlock(buffer, 0, bytesRead, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
        }

        private static CacheEntry ReadRecord(SqliteDataReader reader)
        {
            return new CacheEntry
            {
                Key = reader.GetString(reader.GetOrdinal("Key")),
                FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
                EntryType = reader.GetString(reader.GetOrdinal("EntryType")),
                Version = reader.GetString(reader.GetOrdinal("Version")),
                SizeBytes = reader.GetInt64(reader.GetOrdinal("SizeBytes")),
                Sha256Hash = reader.GetString(reader.GetOrdinal("Sha256Hash")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                LastAccessedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastAccessedAt"))),
                ExpiresAt = reader.IsDBNull(reader.GetOrdinal("ExpiresAt"))
                    ? null
                    : DateTime.Parse(reader.GetString(reader.GetOrdinal("ExpiresAt"))),
                IsLocked = reader.GetInt32(reader.GetOrdinal("IsLocked")) == 1,
                IsValid = reader.GetInt32(reader.GetOrdinal("IsValid")) == 1
            };
        }
    }
}
