using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using SayraClient.Services;

namespace SayraClient.RemoteOperations.Handlers
{
    public class ShutdownCommandHandler : IRemoteCommandHandler
    {
        private readonly IPowerManagementService _powerManagement;

        public ShutdownCommandHandler(IPowerManagementService powerManagement)
        {
            _powerManagement = powerManagement;
        }

        public bool CanHandle(string action) => action.Equals("SHUTDOWN", StringComparison.OrdinalIgnoreCase);

        public async Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            try
            {
                await _powerManagement.ShutdownAsync(cancellationToken);
                return CommandResult.Successful(command.CommandId, "Shutdown initiated successfully");
            }
            catch (Exception ex)
            {
                return CommandResult.Failed(command.CommandId, "SHUTDOWN_FAILED", ex.Message);
            }
        }
    }
}
