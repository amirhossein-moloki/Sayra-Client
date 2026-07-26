using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Launcher.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Handlers
{
    public class LaunchGameCommandHandler : IRemoteCommandHandler
    {
        private readonly IGameLauncherService _gameLauncher;

        public LaunchGameCommandHandler(IGameLauncherService gameLauncher)
        {
            _gameLauncher = gameLauncher;
        }

        public bool CanHandle(string action) => action.Equals("LAUNCH_GAME", StringComparison.OrdinalIgnoreCase);

        public async Task<CommandResult> HandleAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            try
            {
                string? gameId = null;
                if (!string.IsNullOrEmpty(command.Payload))
                {
                    using var doc = JsonDocument.Parse(command.Payload);
                    if (doc.RootElement.TryGetProperty("gameId", out var prop))
                    {
                        gameId = prop.GetString();
                    }
                }

                if (string.IsNullOrEmpty(gameId))
                {
                    return CommandResult.Failed(command.CommandId, "INVALID_PAYLOAD", "gameId is missing from the payload");
                }

                bool result = await _gameLauncher.LaunchGameAsync(gameId, cancellationToken);
                if (result)
                {
                    return CommandResult.Successful(command.CommandId, $"Game {gameId} launched successfully");
                }
                else
                {
                    return CommandResult.Failed(command.CommandId, "LAUNCH_FAILED", $"Failed to launch game {gameId}");
                }
            }
            catch (Exception ex)
            {
                return CommandResult.Failed(command.CommandId, "LAUNCH_ERROR", ex.Message);
            }
        }
    }
}
