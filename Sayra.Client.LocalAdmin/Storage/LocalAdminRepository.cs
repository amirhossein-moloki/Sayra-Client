using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.LocalAdmin.Storage
{
    public class LocalAdminRepository : ILocalAdminRepository
    {
        private readonly string _basePath;
        private readonly string _filePath;
        private readonly string _backupPath;
        private readonly ILogger<LocalAdminRepository>? _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public LocalAdminRepository(string? basePath = null, ILogger<LocalAdminRepository>? logger = null)
        {
            _basePath = basePath != null
                ? Path.Combine(basePath, "LocalAdmin")
                : Path.Combine(AppContext.BaseDirectory, "Data", "LocalAdmin");
            _filePath = Path.Combine(_basePath, "admin_credentials.json");
            _backupPath = Path.Combine(_basePath, "admin_credentials.json.bak");
            _logger = logger;
        }

        public async Task<IEnumerable<LocalAdminCredential>> LoadCredentialsAsync()
        {
            EnsureDirectoryExists();

            if (File.Exists(_filePath))
            {
                try
                {
                    using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                    {
                        var result = await JsonSerializer.DeserializeAsync<List<LocalAdminCredential>>(stream, JsonOptions);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load credentials file {FilePath}. Attempting recovery from backup.", _filePath);
                }
            }

            if (File.Exists(_backupPath))
            {
                try
                {
                    using (var stream = new FileStream(_backupPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                    {
                        var result = await JsonSerializer.DeserializeAsync<List<LocalAdminCredential>>(stream, JsonOptions);
                        if (result != null)
                        {
                            _logger?.LogInformation("Successfully recovered credentials from backup {BackupPath}", _backupPath);
                            try
                            {
                                File.Copy(_backupPath, _filePath, overwrite: true);
                            }
                            catch (Exception copyEx)
                            {
                                _logger?.LogWarning(copyEx, "Could not restore backup to {FilePath}.", _filePath);
                            }
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load credentials backup file {BackupPath} as well.", _backupPath);
                }
            }

            return new List<LocalAdminCredential>();
        }

        public async Task SaveCredentialsAsync(IEnumerable<LocalAdminCredential> credentials)
        {
            EnsureDirectoryExists();

            string tempPath = _filePath + ".tmp";

            // 1. Write to temp file asynchronously
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, credentials, JsonOptions);
                await stream.FlushAsync();
            }

            // 2. Backup existing file before replacement
            if (File.Exists(_filePath))
            {
                try
                {
                    if (File.Exists(_backupPath))
                    {
                        File.Delete(_backupPath);
                    }
                    File.Copy(_filePath, _backupPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not create backup of credentials at {BackupPath}", _backupPath);
                }
            }

            // 3. Atomic replace
            try
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }
                File.Move(tempPath, _filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed atomic write for credentials. Falling back to copy.");
                if (File.Exists(tempPath))
                {
                    File.Copy(tempPath, _filePath, overwrite: true);
                    File.Delete(tempPath);
                }
            }
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }
    }
}
