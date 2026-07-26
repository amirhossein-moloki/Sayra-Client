using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using SayraClient.Kiosk.Application.Interfaces;

namespace SayraClient.RemoteOperations.Handlers
{
    public class MaintenanceModeCommandHandler : IRemoteCommandHandler
    {
        private readonly IMaintenanceModeService _maintenanceModeService;

        public MaintenanceModeCommandHandler(IMaintenanceModeService maintenanceModeService)
        {
            _maintenanceModeService = maintenanceModeService;
        }

        public bool CanHandle(string action) => action.Equals("MAINTENANCE_MODE", StringComparison.OrdinalIgnoreCase);

        public async Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotImplementedException("Maintenance Mode kiosk shell restoration relies on native Win32 API Explorer hooks and registry modifications only available on Windows.");
            }

            try
            {
                bool enable = true;
                string password = "Admin123!"; // Default or parsed from payload

                if (!string.IsNullOrEmpty(command.Payload))
                {
                    using var doc = JsonDocument.Parse(command.Payload);
                    if (doc.RootElement.TryGetProperty("enable", out var enableProp))
                    {
                        enable = enableProp.GetBoolean();
                    }
                    if (doc.RootElement.TryGetProperty("password", out var passProp))
                    {
                        password = passProp.GetString() ?? password;
                    }
                }

                if (enable)
                {
                    bool entered = await _maintenanceModeService.EnterMaintenanceModeAsync(password);
                    if (entered)
                    {
                        return CommandResult.Successful(command.CommandId, "Successfully entered maintenance mode.");
                    }
                    else
                    {
                        return CommandResult.Failed(command.CommandId, "AUTH_FAILED", "Failed to authenticate administrator credentials for maintenance mode.");
                    }
                }
                else
                {
                    _maintenanceModeService.ExitMaintenanceMode();
                    return CommandResult.Successful(command.CommandId, "Successfully exited maintenance mode.");
                }
            }
            catch (Exception ex)
            {
                return CommandResult.Failed(command.CommandId, "MAINTENANCE_FAILED", ex.Message);
            }
        }
    }
}
