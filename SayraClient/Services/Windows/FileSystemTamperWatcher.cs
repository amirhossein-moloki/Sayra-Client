using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.GameLibrary.Services;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Services.Windows;

public class FileSystemTamperWatcher : SupervisedBackgroundService
{
    private readonly IGameLibraryService _gameLibraryService;
    private readonly IAuditLogger _auditLogger;
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _activeWatchers = new();
    private readonly string _configDirectory;

    public FileSystemTamperWatcher(
        ILogger<FileSystemTamperWatcher> logger,
        IServiceHealthMonitor healthMonitor,
        IGameLibraryService gameLibraryService,
        IAuditLogger auditLogger)
        : base(logger, healthMonitor, "FileSystemTamperWatcher")
    {
        _gameLibraryService = gameLibraryService;
        _auditLogger = auditLogger;
        _configDirectory = AppContext.BaseDirectory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FileSystemTamperWatcher started. Monitoring client configuration folder: {ConfigDir}", _configDirectory);

        // 1. Initialize watcher for local client configuration folder
        StartConfigWatcher();

        // 2. Continuous loop to dynamically monitor and subscribe to registered game folders
        while (!stoppingToken.IsCancellationRequested)
        {
            _healthMonitor.ReportHeartbeat(_serviceName);

            try
            {
                await SyncGameDirectoriesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while syncing game directories for FileSystemTamperWatcher.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private void StartConfigWatcher()
    {
        try
        {
            if (Directory.Exists(_configDirectory))
            {
                var watcher = new FileSystemWatcher(_configDirectory)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                watcher.Changed += OnConfigTamperDetected;
                watcher.Created += OnConfigTamperDetected;
                watcher.Deleted += OnConfigTamperDetected;
                watcher.Renamed += OnConfigTamperRenamed;

                _activeWatchers.TryAdd(_configDirectory.ToLowerInvariant(), watcher);
                _logger.LogInformation("Config folder FileSystemWatcher registered successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FileSystemWatcher for local config folder.");
        }
    }

    private async Task SyncGameDirectoriesAsync()
    {
        var games = await _gameLibraryService.GetGames();
        if (games == null) return;

        foreach (var game in games)
        {
            string? targetDir = null;

            if (!string.IsNullOrEmpty(game.WorkingDirectory) && Directory.Exists(game.WorkingDirectory))
            {
                targetDir = game.WorkingDirectory;
            }
            else if (!string.IsNullOrEmpty(game.ExecutablePath))
            {
                try
                {
                    targetDir = Path.GetDirectoryName(game.ExecutablePath);
                }
                catch
                {
                    // Ignore malformed paths
                }
            }

            if (targetDir == null || !Directory.Exists(targetDir)) continue;

            string key = targetDir.ToLowerInvariant();
            if (!_activeWatchers.ContainsKey(key))
            {
                try
                {
                    var watcher = new FileSystemWatcher(targetDir)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true
                    };

                    watcher.Changed += (s, e) => OnGameFolderTamperDetected(game.Name, e);
                    watcher.Created += (s, e) => OnGameFolderTamperDetected(game.Name, e);
                    watcher.Deleted += (s, e) => OnGameFolderTamperDetected(game.Name, e);
                    watcher.Renamed += (s, e) => OnGameFolderTamperRenamed(game.Name, e);

                    if (_activeWatchers.TryAdd(key, watcher))
                    {
                        _logger.LogInformation("FileSystemWatcher started for Game '{GameName}' directory: {Path}", game.Name, targetDir);
                    }
                    else
                    {
                        watcher.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not start FileSystemWatcher for game directory: {Path}", targetDir);
                }
            }
        }
    }

    private void OnConfigTamperDetected(object sender, FileSystemEventArgs e)
    {
        if (IsCriticalExtension(e.FullPath))
        {
            string message = $"Tamper detection alert: Critical client configuration file modified, created, or deleted! File: {e.FullPath}, ChangeType: {e.ChangeType}";
            _logger.LogWarning("SECURITY CRITICAL: {Message}", message);
            _auditLogger.LogSecurity(message);
        }
    }

    private void OnConfigTamperRenamed(object sender, RenamedEventArgs e)
    {
        if (IsCriticalExtension(e.OldFullPath) || IsCriticalExtension(e.FullPath))
        {
            string message = $"Tamper detection alert: Critical client configuration file renamed! From {e.OldFullPath} to {e.FullPath}";
            _logger.LogWarning("SECURITY CRITICAL: {Message}", message);
            _auditLogger.LogSecurity(message);
        }
    }

    private void OnGameFolderTamperDetected(string gameName, FileSystemEventArgs e)
    {
        if (IsCriticalExtension(e.FullPath))
        {
            string message = $"Tamper detection alert: Game '{gameName}' installation file modified, created, or deleted! File: {e.FullPath}, ChangeType: {e.ChangeType}";
            _logger.LogWarning("SECURITY CRITICAL: {Message}", message);
            _auditLogger.LogSecurity(message);
        }
    }

    private void OnGameFolderTamperRenamed(string gameName, RenamedEventArgs e)
    {
        if (IsCriticalExtension(e.OldFullPath) || IsCriticalExtension(e.FullPath))
        {
            string message = $"Tamper detection alert: Game '{gameName}' installation file renamed! From {e.OldFullPath} to {e.FullPath}";
            _logger.LogWarning("SECURITY CRITICAL: {Message}", message);
            _auditLogger.LogSecurity(message);
        }
    }

    private bool IsCriticalExtension(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".exe" || ext == ".dll" || ext == ".json" || ext == ".config" || ext == ".key" || ext == ".db" || ext == ".sys";
    }

    public override void Dispose()
    {
        foreach (var watcher in _activeWatchers.Values)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch
            {
                // Suppress dispose exceptions
            }
        }
        _activeWatchers.Clear();
        base.Dispose();
    }
}
