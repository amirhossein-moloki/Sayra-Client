using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IRemoteCommandEngine
    {
        Task StartEngineAsync(CancellationToken cancellationToken);
        Task QueueCommandAsync(RemoteCommand command);
        Task<CommandStatus> GetCommandStatusAsync(Guid commandId);
    }
}
