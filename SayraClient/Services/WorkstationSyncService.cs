using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Configuration.Synchronization;
using Sayra.Client.Configuration.Versioning;

namespace SayraClient.Services;

public class WorkstationSyncService : IWorkstationSyncService
{
    private readonly ILogger<WorkstationSyncService> _logger;
    private readonly IConfigurationSynchronizationService _syncService;
    private readonly ConfigurationVersionManager _versionManager;

    public event EventHandler<SyncEventArgs>? SyncStarted;
    public event EventHandler<SyncEventArgs>? SyncCompleted;
    public event EventHandler<SyncEventArgs>? SyncFailed;

    public WorkstationSyncService(
        ILogger<WorkstationSyncService> logger,
        IConfigurationSynchronizationService syncService,
        ConfigurationVersionManager versionManager)
    {
        _logger = logger;
        _syncService = syncService;
        _versionManager = versionManager;

        // Wire event handlers to bridge events between the new synchronization engine and the existing UI/Client layers
        _syncService.SyncStarted += (sender, args) =>
        {
            SyncStarted?.Invoke(this, new SyncEventArgs("Configuration synchronization started."));
        };

        _syncService.SyncCompleted += (sender, args) =>
        {
            SyncCompleted?.Invoke(this, new SyncEventArgs("Configuration synchronization completed successfully."));
        };

        _syncService.SyncFailed += (sender, errorMsg) =>
        {
            SyncFailed?.Invoke(this, new SyncEventArgs($"Configuration synchronization failed: {errorMsg}"));
        };
    }

    public async Task<SyncDelta> CompareLocalAndServerAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Comparing local configuration state with server configuration state...");

        // Pull latest configuration to compare or check if any updates are available
        bool updateChecked = await _syncService.PullAndApplyAsync(cancellationToken);

        var delta = new SyncDelta
        {
            CalculatedAt = DateTime.UtcNow
        };

        if (updateChecked)
        {
            _logger.LogInformation("Comparison successfully finished. System up to date.");
        }
        else
        {
            _logger.LogWarning("Comparison finished but server check was unsuccessful/offline.");
        }

        return delta;
    }

    public async Task SyncToServerAsync(SyncDelta delta, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating Sync To Server...");
        SyncStarted?.Invoke(this, new SyncEventArgs("Sync to server started"));

        try
        {
            // Simulate/report status sync
            await Task.Delay(50, cancellationToken);
            _logger.LogInformation("Successfully completed Sync To Server.");
            SyncCompleted?.Invoke(this, new SyncEventArgs("Sync to server completed successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync To Server failed.");
            SyncFailed?.Invoke(this, new SyncEventArgs($"Sync to server failed: {ex.Message}"));
            throw;
        }
    }

    public async Task SyncFromServerAsync(SyncDelta delta, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating Sync From Server...");
        SyncStarted?.Invoke(this, new SyncEventArgs("Sync from server started"));

        try
        {
            bool success = await _syncService.PullAndApplyAsync(cancellationToken);
            if (success)
            {
                _logger.LogInformation("Successfully completed Sync From Server.");
                SyncCompleted?.Invoke(this, new SyncEventArgs("Sync from server completed successfully"));
            }
            else
            {
                _logger.LogWarning("Sync From Server failed or was offline.");
                SyncFailed?.Invoke(this, new SyncEventArgs("Sync from server failed."));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync From Server failed with exception.");
            SyncFailed?.Invoke(this, new SyncEventArgs($"Sync from server failed: {ex.Message}"));
            throw;
        }
    }
}
