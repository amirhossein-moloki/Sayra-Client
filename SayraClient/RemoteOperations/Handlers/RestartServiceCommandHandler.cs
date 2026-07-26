using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Handlers
{
    public class RestartServiceCommandHandler : IRemoteCommandHandler
    {
        public bool CanHandle(string action) => action.Equals("RESTART_SERVICE", StringComparison.OrdinalIgnoreCase);

        public Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Restarting the background NT Windows Service requires elevated SCM (Service Control Manager) access privileges and a separate watchdog launcher process.");
        }
    }
}
