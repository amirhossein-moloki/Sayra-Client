using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sayra.Client.Configuration.Versioning;

public class VersionHistoryEntry
{
    public long Version { get; set; }
    public DateTime InstalledAt { get; set; }
    public string Status { get; set; } = string.Empty; // e.g. "Applied", "RolledBack"
}

public class ConfigurationVersionManager
{
    private readonly string _historyFilePath;
    private readonly ILogger<ConfigurationVersionManager>? _logger;
    private readonly List<VersionHistoryEntry> _history = new();
    private readonly object _lock = new();

    public ConfigurationVersionManager(string? historyFilePath = null, ILogger<ConfigurationVersionManager>? logger = null)
    {
        _historyFilePath = historyFilePath ?? Path.Combine(AppContext.BaseDirectory, "Data", "Configuration", "version_history.json");
        _logger = logger;
        LoadHistory();
    }

    public long CurrentVersion
    {
        get
        {
            lock (_lock)
            {
                if (_history.Count == 0) return 0;
                for (int i = _history.Count - 1; i >= 0; i--)
                {
                    if (_history[i].Status == "Applied")
                    {
                        return _history[i].Version;
                    }
                }
                return 0;
            }
        }
    }

    public List<VersionHistoryEntry> GetHistory()
    {
        lock (_lock)
        {
            return new List<VersionHistoryEntry>(_history);
        }
    }

    public bool ValidateVersion(long newVersion, out string errorMessage)
    {
        errorMessage = string.Empty;
        long current = CurrentVersion;

        if (newVersion < current)
        {
            errorMessage = $"Downgrade attack detected! Incoming version code '{newVersion}' is lower than active version '{current}'.";
            _logger?.LogWarning(errorMessage);
            return false;
        }

        if (newVersion == current)
        {
            errorMessage = $"Incoming version code '{newVersion}' is equal to active version '{current}'. Update not needed.";
            _logger?.LogInformation(errorMessage);
            return false;
        }

        return true;
    }

    public void RecordVersionChange(long version, string status)
    {
        lock (_lock)
        {
            _history.Add(new VersionHistoryEntry
            {
                Version = version,
                InstalledAt = DateTime.UtcNow,
                Status = status
            });
            SaveHistory();
        }
        _logger?.LogInformation($"Recorded configuration version change to Version: {version}, Status: {status}");
    }

    private void LoadHistory()
    {
        try
        {
            lock (_lock)
            {
                if (File.Exists(_historyFilePath))
                {
                    string json = File.ReadAllText(_historyFilePath);
                    var loaded = JsonSerializer.Deserialize<List<VersionHistoryEntry>>(json);
                    if (loaded != null)
                    {
                        _history.Clear();
                        _history.AddRange(loaded);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load version history. Starting fresh.");
        }
    }

    private void SaveHistory()
    {
        try
        {
            lock (_lock)
            {
                string? dir = Path.GetDirectoryName(_historyFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyFilePath, json);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save version history to file.");
        }
    }
}
