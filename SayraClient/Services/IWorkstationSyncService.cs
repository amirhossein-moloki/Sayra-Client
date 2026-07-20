using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SayraClient.Services;

public class SyncDelta
{
    public List<string> AddedLocalIds { get; set; } = new();
    public List<string> UpdatedLocalIds { get; set; } = new();
    public List<string> DeletedLocalIds { get; set; } = new();
    public List<string> AddedServerIds { get; set; } = new();
    public List<string> UpdatedServerIds { get; set; } = new();
    public List<string> DeletedServerIds { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public class SyncEventArgs : EventArgs
{
    public string Status { get; }
    public SyncEventArgs(string status) => Status = status;
}

public interface IWorkstationSyncService
{
    event EventHandler<SyncEventArgs>? SyncStarted;
    event EventHandler<SyncEventArgs>? SyncCompleted;
    event EventHandler<SyncEventArgs>? SyncFailed;

    Task<SyncDelta> CompareLocalAndServerAsync(CancellationToken cancellationToken = default);
    Task SyncToServerAsync(SyncDelta delta, CancellationToken cancellationToken = default);
    Task SyncFromServerAsync(SyncDelta delta, CancellationToken cancellationToken = default);
}
