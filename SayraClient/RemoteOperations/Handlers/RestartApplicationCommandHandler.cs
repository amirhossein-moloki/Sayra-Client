using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Handlers
{
    public class RestartApplicationCommandHandler : IRemoteCommandHandler
    {
        public bool CanHandle(string action) => action.Equals("RESTART_APPLICATION", StringComparison.OrdinalIgnoreCase);

        public Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Restarting the WPF Application shell from Session 0 requires platform integration with active Windows Interactive Session (WTS) process spawning.");
        }
    }
}
