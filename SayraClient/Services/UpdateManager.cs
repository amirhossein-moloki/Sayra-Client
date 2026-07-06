using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Ipc;
using Sayra.Client.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;

namespace SayraClient.Services;

public class UpdateManager : BackgroundService
{
    private readonly ILogger<UpdateManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly UpdateVerificationService _verificationService;
    private readonly BackupService _backupService;
    private readonly IpcServer _ipcServer;
    private readonly string _currentVersion;
    private readonly HttpClient _httpClient;
    private readonly string _updateWorkDir;

    public UpdateManager(
        ILogger<UpdateManager> logger,
        IConfiguration configuration,
        SessionManager sessionManager,
        UpdateVerificationService verificationService,
        BackupService backupService,
        IpcServer ipcServer)
    {
        _logger = logger;
        _configuration = configuration;
        _sessionManager = sessionManager;
        _verificationService = verificationService;
        _backupService = backupService;
        _ipcServer = ipcServer;
        _currentVersion = typeof(UpdateManager).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        _httpClient = new HttpClient();
        _updateWorkDir = Path.Combine(AppContext.BaseDirectory, "Updates");

        if (!Directory.Exists(_updateWorkDir))
        {
            Directory.CreateDirectory(_updateWorkDir);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool autoUpdate = _configuration.GetValue<bool>("UpdateConfig:AutoUpdate", true);
        int checkIntervalMinutes = _configuration.GetValue<int>("UpdateConfig:CheckIntervalMinutes", 60);

        if (!autoUpdate)
        {
            _logger.LogInformation("Auto-update is disabled.");
            return;
        }

        _logger.LogInformation("UpdateManager started. Current version: {version}", _currentVersion);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndApplyUpdateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in update lifecycle.");
            }

            await Task.Delay(TimeSpan.FromMinutes(checkIntervalMinutes), stoppingToken);
        }
    }

    private async Task CheckAndApplyUpdateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking for updates...");

        var manifest = await FetchUpdateManifestAsync(cancellationToken);
        if (manifest == null) return;

        if (IsNewerVersion(manifest.Version))
        {
            _logger.LogInformation("New update available: {Version}", manifest.Version);
            await _ipcServer.BroadcastEventAsync(IpcMessageType.UPDATE_AVAILABLE, JsonSerializer.Serialize(manifest));

            // Only update if no active session
            if (_sessionManager.CurrentStatus != "IDLE")
            {
                _logger.LogInformation("Update deferred: A session is currently active.");
                return;
            }

            await PerformUpdateAsync(manifest, cancellationToken);
        }
    }

    private async Task<UpdateManifest?> FetchUpdateManifestAsync(CancellationToken cancellationToken)
    {
        string updateUrl = _configuration["UpdateConfig:UpdateUrl"] ?? "http://127.0.0.1:5000/api/updates/manifest";
        try
        {
            // In offline environments, this might point to a local server or a mounted USB path
            if (updateUrl.StartsWith("http"))
            {
                return await _httpClient.GetFromJsonAsync<UpdateManifest>(updateUrl, cancellationToken);
            }
            else if (File.Exists(updateUrl))
            {
                string json = await File.ReadAllTextAsync(updateUrl, cancellationToken);
                return JsonSerializer.Deserialize<UpdateManifest>(json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to fetch update manifest: {Message}", ex.Message);
        }
        return null;
    }

    private bool IsNewerVersion(string newVersion)
    {
        if (Version.TryParse(newVersion, out var vNew) && Version.TryParse(_currentVersion, out var vCurr))
        {
            return vNew > vCurr;
        }
        return false;
    }

    private async Task PerformUpdateAsync(UpdateManifest manifest, CancellationToken cancellationToken)
    {
        try
        {
            await _ipcServer.BroadcastEventAsync(IpcMessageType.UPDATE_STARTED, manifest.Version);
            _logger.LogInformation("Starting update to version {Version}", manifest.Version);

            // 1. Download
            string packagePath = Path.Combine(_updateWorkDir, $"update_{manifest.Version}.zip");
            await DownloadPackageAsync(manifest.PackageUrl, packagePath, cancellationToken);

            // 2. Verify
            if (!_verificationService.VerifyPackage(packagePath, manifest.Checksum, manifest.Signature))
            {
                throw new Exception("Update package verification failed (Checksum/Signature mismatch).");
            }

            // 3. Backup
            string? backupPath = await _backupService.CreateBackupAsync(_currentVersion);
            if (backupPath == null)
            {
                throw new Exception("Failed to create backup before update.");
            }

            // 4. Trigger Updater Utility
            await _ipcServer.BroadcastEventAsync(IpcMessageType.UPDATE_PROGRESS, JsonSerializer.Serialize(new UpdateProgressPayload
            {
                Version = manifest.Version,
                ProgressPercentage = 100,
                CurrentAction = "Restarting to apply updates..."
            }));

            _logger.LogInformation("Handing over to Updater utility...");
            LaunchUpdater(packagePath, backupPath);

            // 5. Exit so Updater can replace files
            // Environment.Exit(0) will be called by the host when it receives the stop signal or we can force it.
            // But usually we should allow the host to shut down gracefully.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed.");
            await _ipcServer.BroadcastEventAsync(IpcMessageType.UPDATE_FAILED, ex.Message);
        }
    }

    private async Task DownloadPackageAsync(string url, string destPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Downloading update package from {Url}", url);

        // Simulating progress
        await _ipcServer.BroadcastEventAsync(IpcMessageType.UPDATE_PROGRESS, JsonSerializer.Serialize(new UpdateProgressPayload
        {
            ProgressPercentage = 10,
            CurrentAction = "Downloading package..."
        }));

        if (url.StartsWith("http"))
        {
            var data = await _httpClient.GetByteArrayAsync(url, cancellationToken);
            await File.WriteAllBytesAsync(destPath, data, cancellationToken);
        }
        else if (File.Exists(url))
        {
            File.Copy(url, destPath, true);
        }
        else
        {
            throw new FileNotFoundException("Update package source not found", url);
        }
    }

    private void LaunchUpdater(string packagePath, string backupPath)
    {
        string updaterExe = Path.Combine(AppContext.BaseDirectory, "SayraUpdater.exe");
        if (!File.Exists(updaterExe))
        {
            _logger.LogError("Updater utility not found at {Path}", updaterExe);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = updaterExe,
            Arguments = $"--package \"{packagePath}\" --backup \"{backupPath}\" --service \"Sayra Client\"",
            UseShellExecute = true,
            Verb = "runas" // Request elevation
        };

        Process.Start(startInfo);
    }
}
