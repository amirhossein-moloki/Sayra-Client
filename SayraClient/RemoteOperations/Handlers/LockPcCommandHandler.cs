using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using SayraClient.Services;

namespace SayraClient.RemoteOperations.Handlers
{
    public class LockPcCommandHandler : IRemoteCommandHandler
    {
        private readonly IPowerManagementService _powerManagement;

        public LockPcCommandHandler(IPowerManagementService powerManagement)
        {
            _powerManagement = powerManagement;
        }

        public bool CanHandle(string action) => action.Equals("LOCK_PC", StringComparison.OrdinalIgnoreCase);

        public async Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            try
            {
                await _powerManagement.LockWorkstationAsync(cancellationToken);
                return CommandResult.Successful(command.CommandId, "Workstation locked successfully");
            }
            catch (Exception ex)
            {
                return CommandResult.Failed(command.CommandId, "LOCK_FAILED", ex.Message);
            }
        }
    }
}
