using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using SayraClient.Services;

namespace SayraClient.RemoteOperations.Handlers
{
    public class RestartCommandHandler : IRemoteCommandHandler
    {
        private readonly IPowerManagementService _powerManagement;

        public RestartCommandHandler(IPowerManagementService powerManagement)
        {
            _powerManagement = powerManagement;
        }

        public bool CanHandle(string action) => action.Equals("RESTART", StringComparison.OrdinalIgnoreCase);

        public async Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            try
            {
                await _powerManagement.RestartAsync(cancellationToken);
                return CommandResult.Successful(command.CommandId, "Restart initiated successfully");
            }
            catch (Exception ex)
            {
                return CommandResult.Failed(command.CommandId, "RESTART_FAILED", ex.Message);
            }
        }
    }
}
