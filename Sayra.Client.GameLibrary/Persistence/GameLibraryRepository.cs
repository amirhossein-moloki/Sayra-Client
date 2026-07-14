using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.GameLibrary.Models;

namespace Sayra.Client.GameLibrary.Persistence
{
    public class GameLibraryRepository : IGameLibraryRepository
    {
        private readonly string _basePath;
        private readonly string _gamesFilePath;
        private readonly string _gamesBackupPath;
        private readonly string _applicationsFilePath;
        private readonly string _applicationsBackupPath;
        private readonly ILogger<GameLibraryRepository>? _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public GameLibraryRepository(string? basePath = null, ILogger<GameLibraryRepository>? logger = null)
        {
            _basePath = basePath ?? Path.Combine(AppContext.BaseDirectory, "Data", "GameLibrary");
            _gamesFilePath = Path.Combine(_basePath, "games.json");
            _gamesBackupPath = Path.Combine(_basePath, "games.json.bak");
            _applicationsFilePath = Path.Combine(_basePath, "applications.json");
            _applicationsBackupPath = Path.Combine(_basePath, "applications.json.bak");
            _logger = logger;
        }

        public async Task<IEnumerable<Game>> GetGamesAsync()
        {
            return await LoadListAsync<Game>(_gamesFilePath, _gamesBackupPath);
        }

        public async Task SaveGamesAsync(IEnumerable<Game> games)
        {
            await SaveListAsync(_gamesFilePath, _gamesBackupPath, games);
        }

        public async Task<IEnumerable<Application>> GetApplicationsAsync()
        {
            return await LoadListAsync<Application>(_applicationsFilePath, _applicationsBackupPath);
        }

        public async Task SaveApplicationsAsync(IEnumerable<Application> applications)
        {
            await SaveListAsync(_applicationsFilePath, _applicationsBackupPath, applications);
        }

        private async Task<List<T>> LoadListAsync<T>(string filePath, string backupPath)
        {
            EnsureDirectoryExists();

            // Try loading main file
            if (File.Exists(filePath))
            {
                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                    {
                        var result = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load main file {FilePath} due to corruption or read error. Attempting recovery from backup.", filePath);
                }
            }

            // Fallback to backup file
            if (File.Exists(backupPath))
            {
                try
                {
                    using (var stream = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                    {
                        var result = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions);
                        if (result != null)
                        {
                            _logger?.LogInformation("Successfully recovered data from backup {BackupPath}", backupPath);
                            // Recover main file from backup
                            try
                            {
                                File.Copy(backupPath, filePath, overwrite: true);
                            }
                            catch (Exception copyEx)
                            {
                                _logger?.LogWarning(copyEx, "Could not restore backup to {FilePath} path.", filePath);
                            }
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load backup file {BackupPath} as well.", backupPath);
                }
            }

            return new List<T>();
        }

        private async Task SaveListAsync<T>(string filePath, string backupPath, IEnumerable<T> list)
        {
            EnsureDirectoryExists();

            string tempPath = filePath + ".tmp";

            // 1. Write to temp file asynchronously
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, list, JsonOptions);
                await stream.FlushAsync();
            }

            // 2. Backup existing file before overwrite
            if (File.Exists(filePath))
            {
                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Copy(filePath, backupPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not create backup of {FilePath} at {BackupPath}", filePath, backupPath);
                }
            }

            // 3. Atomic file replacement
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tempPath, filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed atomic file replacement for {FilePath}. Falling back to copy.", filePath);
                if (File.Exists(tempPath))
                {
                    File.Copy(tempPath, filePath, overwrite: true);
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
