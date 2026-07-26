using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IRemoteCommandHandler
    {
        Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken);
        bool CanHandle(string action);
    }
}
