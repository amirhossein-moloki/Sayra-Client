using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Scanner.Models;

namespace Sayra.Client.Scanner.Cache
{
    public interface IScanCacheService
    {
        Task LoadAsync();
        Task SaveAsync();
        bool TryGet(string filePath, out DetectedApplication? application);
        void Set(string filePath, DetectedApplication application);
        void Clear();
    }

    public class ScanCacheService : IScanCacheService
    {
        private readonly string _filePath;
        private readonly ILogger<ScanCacheService>? _logger;
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public class CacheEntry
        {
            public string ExecutablePath { get; set; } = string.Empty;
            public string FileHash { get; set; } = string.Empty;
            public DateTime LastWriteTime { get; set; }
            public long FileSize { get; set; }
            public DetectedApplication Application { get; set; } = new();
        }

        public ScanCacheService(string? basePath = null, ILogger<ScanCacheService>? logger = null)
        {
            string dir = basePath ?? Path.Combine(AppContext.BaseDirectory, "Data", "Scanner");
            _filePath = Path.Combine(dir, "scan_cache.json");
            _logger = logger;
        }

        public async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    _cache.Clear();
                    return;
                }

                using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var loaded = await JsonSerializer.DeserializeAsync<List<CacheEntry>>(stream, JsonOptions);
                _cache.Clear();
                if (loaded != null)
                {
                    foreach (var entry in loaded)
                    {
                        if (!string.IsNullOrWhiteSpace(entry.ExecutablePath))
                        {
                            _cache[entry.ExecutablePath] = entry;
                        }
                    }
                }
                _logger?.LogInformation("Loaded {Count} entries from scan cache.", _cache.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load scan cache from {Path}", _filePath);
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_filePath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var list = new List<CacheEntry>(_cache.Values);
                using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await JsonSerializer.SerializeAsync(stream, list, JsonOptions);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save scan cache to {Path}", _filePath);
            }
        }

        public bool TryGet(string filePath, out DetectedApplication? application)
        {
            application = null;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            if (_cache.TryGetValue(filePath, out var entry))
            {
                var info = new FileInfo(filePath);
                // Check change indicators: File Size & Last Write Time
                if (info.Length == entry.FileSize && info.LastWriteTimeUtc == entry.LastWriteTime)
                {
                    application = entry.Application;
                    return true;
                }
            }

            return false;
        }

        public void Set(string filePath, DetectedApplication application)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || application == null)
            {
                return;
            }

            var info = new FileInfo(filePath);
            var entry = new CacheEntry
            {
                ExecutablePath = filePath,
                FileHash = application.ExecutableHash,
                FileSize = info.Length,
                LastWriteTime = info.LastWriteTimeUtc,
                Application = application
            };

            _cache[filePath] = entry;
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
