using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Launcher.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Handlers
{
    public class KillProcessCommandHandler : IRemoteCommandHandler
    {
        private readonly IGameLauncherService _gameLauncher;

        public KillProcessCommandHandler(IGameLauncherService gameLauncher)
        {
            _gameLauncher = gameLauncher;
        }

        public bool CanHandle(string action) => action.Equals("KILL_PROCESS", StringComparison.OrdinalIgnoreCase);

        public async Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            try
            {
                int pid = 0;
                string? name = null;

                if (!string.IsNullOrEmpty(command.Payload))
                {
                    using var doc = JsonDocument.Parse(command.Payload);
                    if (doc.RootElement.TryGetProperty("pid", out var pidProp) && pidProp.TryGetInt32(out int p))
                    {
                        pid = p;
                    }
                    if (doc.RootElement.TryGetProperty("name", out var nameProp))
                    {
                        name = nameProp.GetString();
                    }
                }

                if (pid > 0)
                {
                    await _gameLauncher.KillProcessAsync(pid);
                    return CommandResult.Successful(command.CommandId, $"Process with PID {pid} killed successfully");
                }
                else if (!string.IsNullOrEmpty(name))
                {
                    await _gameLauncher.KillProcessByNameAsync(name);
                    return CommandResult.Successful(command.CommandId, $"Process {name} killed successfully");
                }
                else
                {
                    return CommandResult.Failed(command.CommandId, "INVALID_PAYLOAD", "Missing pid or name in payload");
                }
            }
            catch (Exception ex)
            {
                return CommandResult.Failed(command.CommandId, "KILL_ERROR", ex.Message);
            }
        }
    }
}
