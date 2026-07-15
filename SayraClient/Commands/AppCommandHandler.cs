using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SayraClient.Models;
using Sayra.Client.Launcher.Services;
using Sayra.Client.GameLibrary.Services;

namespace SayraClient.Commands;

public class AppCommandHandler : ICommandHandler
{
    private readonly IGameLauncherService _gameLauncher;
    private readonly IGameLibraryService _gameLibrary;
    private readonly ILogger<AppCommandHandler> _logger;

    public AppCommandHandler(
        IGameLauncherService gameLauncher,
        IGameLibraryService gameLibrary,
        ILogger<AppCommandHandler> logger)
    {
        _gameLauncher = gameLauncher;
        _gameLibrary = gameLibrary;
        _logger = logger;
    }

    public bool CanHandle(string action)
    {
        return action.ToUpper() switch
        {
            "RUN_APP" or "KILL_APP" or "LIST_PROCESSES" => true,
            _ => false
        };
    }

    public async Task<ExecutionResult?> HandleAsync(CommandModel command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling action: {action}", command.Action);

        var result = command.Action.ToUpper() switch
        {
            "RUN_APP" => await HandleRunAppAsync(command.Payload, cancellationToken),
            "KILL_APP" => await HandleKillAppAsync(command.Payload),
            "LIST_PROCESSES" => HandleListProcesses(),
            _ => ExecutionResult.Error(command.Action, "Unsupported action")
        };

        if (result != null)
        {
            _logger.LogInformation("Action {action} completed with status: {status}", command.Action, result.Status);
        }

        return result;
    }

    private async Task<ExecutionResult> HandleRunAppAsync(object? payload, CancellationToken cancellationToken)
    {
        try
        {
            if (payload is JsonElement element)
            {
                if (element.TryGetProperty("gameId", out var gameIdProp))
                {
                    string gameId = gameIdProp.GetString() ?? throw new Exception("gameId is null");
                    bool success = await _gameLauncher.LaunchGameAsync(gameId, cancellationToken);
                    if (success)
                    {
                        return ExecutionResult.Success("RUN_APP", "Application started");
                    }
                    else
                    {
                        return ExecutionResult.Error("RUN_APP", "Failed to start application.");
                    }
                }

                if (element.TryGetProperty("path", out var pathProperty))
                {
                    string path = pathProperty.GetString() ?? throw new Exception("Path is null");
                    var games = await _gameLibrary.GetGames();
                    var registered = games
                        .FirstOrDefault(g => g.ExecutablePath.Equals(path, StringComparison.OrdinalIgnoreCase));

                    if (registered != null)
                    {
                        bool success = await _gameLauncher.LaunchGameAsync(registered.Id, cancellationToken);
                        if (success)
                        {
                            return ExecutionResult.Success("RUN_APP", "Application started from registry");
                        }
                    }

                    return ExecutionResult.Error("RUN_APP", "Direct path execution is not allowed. Please use GameId.");
                }
            }

            return ExecutionResult.Error("RUN_APP", "Invalid payload: missing gameId or path");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing RUN_APP");
            return ExecutionResult.Error("RUN_APP", ex.Message);
        }
    }

    private async Task<ExecutionResult> HandleKillAppAsync(object? payload)
    {
        try
        {
            if (payload is JsonElement element)
            {
                if (element.TryGetProperty("pid", out var pidProperty) && pidProperty.TryGetInt32(out int pid))
                {
                    await _gameLauncher.KillProcessAsync(pid);
                    return ExecutionResult.Success("KILL_APP");
                }

                if (element.TryGetProperty("name", out var nameProperty))
                {
                    string name = nameProperty.GetString() ?? throw new Exception("Name is null");
                    await _gameLauncher.KillProcessByNameAsync(name);
                    return ExecutionResult.Success("KILL_APP");
                }
            }

            return ExecutionResult.Error("KILL_APP", "Invalid payload: missing pid or name");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing KILL_APP");
            return ExecutionResult.Error("KILL_APP", ex.Message);
        }
    }

    private ExecutionResult HandleListProcesses()
    {
        try
        {
            var processes = _gameLauncher.GetRunningGames();
            return ExecutionResult.Success("LIST_PROCESSES", result: JsonSerializer.Serialize(processes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing LIST_PROCESSES");
            return ExecutionResult.Error("LIST_PROCESSES", ex.Message);
        }
    }
}
