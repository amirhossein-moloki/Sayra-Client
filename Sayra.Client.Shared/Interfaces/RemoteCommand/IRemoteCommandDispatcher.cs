using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IRemoteCommandDispatcher
    {
        Task<CommandResult> DispatchAsync(RemoteCommand command, CancellationToken cancellationToken);
    }
}
