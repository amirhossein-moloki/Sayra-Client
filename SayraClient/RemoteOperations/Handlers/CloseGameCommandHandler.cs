using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Launcher.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Handlers
{
    public class CloseGameCommandHandler : IRemoteCommandHandler
    {
        private readonly IGameLauncherService _gameLauncher;

        public CloseGameCommandHandler(IGameLauncherService gameLauncher)
        {
            _gameLauncher = gameLauncher;
        }

        public bool CanHandle(string action) => action.Equals("CLOSE_GAME", StringComparison.OrdinalIgnoreCase);

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

                await _gameLauncher.StopGameAsync(gameId);
                return CommandResult.Successful(command.CommandId, $"Game {gameId} closed successfully");
            }
            catch (Exception ex)
            {
                return CommandResult.Failed(command.CommandId, "CLOSE_ERROR", ex.Message);
            }
        }
    }
}
