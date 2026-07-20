using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class WorkstationSyncService : IWorkstationSyncService
{
    private readonly ILogger<WorkstationSyncService> _logger;

    public event EventHandler<SyncEventArgs>? SyncStarted;
    public event EventHandler<SyncEventArgs>? SyncCompleted;
    public event EventHandler<SyncEventArgs>? SyncFailed;

    public WorkstationSyncService(ILogger<WorkstationSyncService> logger)
    {
        _logger = logger;
    }

    public Task<SyncDelta> CompareLocalAndServerAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting comparison between Local and Server systems...");

        // This is a client-side contract entry point. The real implementation requires the server synchronization phase.
        _logger.LogWarning("CompareLocalAndServerAsync: Waiting for Server Synchronization Phase.");

        throw new NotSupportedException("CompareLocalAndServerAsync is waiting for the Server Synchronization Phase.");
    }

    public Task SyncToServerAsync(SyncDelta delta, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating Sync To Server...");
        SyncStarted?.Invoke(this, new SyncEventArgs("Sync to server started"));

        // Requires server implementation.
        _logger.LogWarning("SyncToServerAsync: Waiting for Server Synchronization Phase.");
        SyncFailed?.Invoke(this, new SyncEventArgs("Waiting for Server Synchronization Phase"));

        throw new NotSupportedException("SyncToServerAsync is waiting for the Server Synchronization Phase.");
    }

    public Task SyncFromServerAsync(SyncDelta delta, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating Sync From Server...");
        SyncStarted?.Invoke(this, new SyncEventArgs("Sync from server started"));

        // Requires server implementation.
        _logger.LogWarning("SyncFromServerAsync: Waiting for Server Synchronization Phase.");
        SyncFailed?.Invoke(this, new SyncEventArgs("Waiting for Server Synchronization Phase"));

        throw new NotSupportedException("SyncFromServerAsync is waiting for the Server Synchronization Phase.");
    }

    // Prepared helper method for raising sync completed when the server is connected in the next phase
    private void RaiseSyncCompleted()
    {
        SyncCompleted?.Invoke(this, new SyncEventArgs("Sync completed successfully"));
    }
}
