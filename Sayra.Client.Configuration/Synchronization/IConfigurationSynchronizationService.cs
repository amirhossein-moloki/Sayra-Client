using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Configuration.Models;

namespace Sayra.Client.Configuration.Synchronization;

public interface IConfigurationSynchronizationService
{
    event EventHandler? SyncStarted;
    event EventHandler? SyncCompleted;
    event EventHandler<string>? SyncFailed;

    Task<bool> PullAndApplyAsync(CancellationToken cancellationToken = default);
    Task<bool> PushAndApplyAsync(ConfigurationPackage package, CancellationToken cancellationToken = default);
    Task<bool> ManualSyncAsync(CancellationToken cancellationToken = default);
}
