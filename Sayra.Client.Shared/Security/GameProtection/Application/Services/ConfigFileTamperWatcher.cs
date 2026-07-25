using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Domain.Events;

namespace Sayra.Client.Shared.Security.GameProtection.Application.Services;

public class ConfigFileTamperWatcher : IDisposable
{
    private readonly ILogger<ConfigFileTamperWatcher> _logger;
    private readonly IThreatReporter _threatReporter;
    private FileSystemWatcher? _watcher;
    private readonly string _configDirectory;

    public ConfigFileTamperWatcher(
        ILogger<ConfigFileTamperWatcher> logger,
        IThreatReporter threatReporter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _threatReporter = threatReporter ?? throw new ArgumentNullException(nameof(threatReporter));
        _configDirectory = AppDomain.CurrentDomain.BaseDirectory;
    }

    public void StartWatching()
    {
        try
        {
            if (!Directory.Exists(_configDirectory))
            {
                _logger.LogWarning("Config directory does not exist: {Path}", _configDirectory);
                return;
            }

            _watcher = new FileSystemWatcher(_configDirectory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnTamperDetected;
            _watcher.Created += OnTamperDetected;
            _watcher.Deleted += OnTamperDetected;
            _watcher.Renamed += OnRenameDetected;

            _logger.LogInformation("ConfigFileTamperWatcher registered for: {Path}", _configDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ConfigFileTamperWatcher.");
        }
    }

    private void OnTamperDetected(object sender, FileSystemEventArgs e)
    {
        if (IsCriticalFile(e.FullPath))
        {
            var threatEvent = new TamperingDetectedEvent
            {
                TargetComponent = "Configuration",
                Reason = $"Critical configuration file tampered: {e.FullPath} (ChangeType: {e.ChangeType})",
                Severity = "Critical"
            };

            _threatReporter.ReportThreat(threatEvent);
        }
    }

    private void OnRenameDetected(object sender, RenamedEventArgs e)
    {
        if (IsCriticalFile(e.OldFullPath) || IsCriticalFile(e.FullPath))
        {
            var threatEvent = new TamperingDetectedEvent
            {
                TargetComponent = "Configuration",
                Reason = $"Critical configuration file renamed from {e.OldFullPath} to {e.FullPath}",
                Severity = "Critical"
            };

            _threatReporter.ReportThreat(threatEvent);
        }
    }

    private bool IsCriticalFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        string name = Path.GetFileName(path).ToLowerInvariant();
        return name == "client_config.json" || ext == ".key" || ext == ".config" || ext == ".db";
    }

    public void Dispose()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }
}
