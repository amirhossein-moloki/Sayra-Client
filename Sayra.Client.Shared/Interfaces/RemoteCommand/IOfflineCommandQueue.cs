using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IOfflineCommandQueue
    {
        Task EnqueueOfflineCommandAsync(RemoteCommand command, CancellationToken cancellationToken = default);
        Task RestoreAndResumeQueueAsync(CancellationToken cancellationToken = default);
    }
}
