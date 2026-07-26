using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Handlers
{
    public class WakeOnLanCommandHandler : IRemoteCommandHandler
    {
        public bool CanHandle(string action) => action.Equals("WAKE_ON_LAN", StringComparison.OrdinalIgnoreCase);

        public Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Wake-on-LAN client trigger requires native integration with BIOS/NIC ACPI and WMI configuration interfaces under Windows kernel drivers.");
        }
    }
}
