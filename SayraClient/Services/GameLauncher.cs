using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Ipc;
using Sayra.Client.Shared.Models;

namespace SayraClient.Services;

public class GameLauncher
{
    private readonly ProcessManager _processManager;
    private readonly ILogger<GameLauncher> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Registry of registered games
    private readonly ConcurrentDictionary<string, GameModel> _gameRegistry = new();

    // PID -> GameId mapping for tracking
    private readonly ConcurrentDictionary<int, string> _trackedProcesses = new();

    public GameLauncher(ProcessManager processManager, ILogger<GameLauncher> logger, IServiceProvider serviceProvider)
    {
        _processManager = processManager;
        _logger = logger;
        _serviceProvider = serviceProvider;

        // Initialize with some default games for testing/demo
        InitializeRegistry();
    }

    private void InitializeRegistry()
    {
        var games = new List<GameModel>
        {
            new GameModel { GameId = "1", Name = "Notepad", ExecutablePath = "notepad.exe", Category = "Apps" },
            new GameModel { GameId = "2", Name = "Chrome", ExecutablePath = "chrome.exe", Category = "Apps" },
            new GameModel { GameId = "3", Name = "Calculator", ExecutablePath = "calc.exe", Category = "Apps" }
        };

        foreach (var game in games)
        {
            _gameRegistry.TryAdd(game.GameId, game);
        }
    }

    public void RegisterGame(GameModel game)
    {
        if (string.IsNullOrEmpty(game.GameId)) throw new ArgumentException("GameId cannot be empty");
        _gameRegistry[game.GameId] = game;
    }

    public IEnumerable<GameModel> GetRegisteredGames() => _gameRegistry.Values;

    public void LaunchGame(string gameId)
    {
        if (!_gameRegistry.TryGetValue(gameId, out var game))
        {
            _logger.LogWarning("Attempted to launch unregistered game: {gameId}", gameId);
            _ = NotifyIpcAsync(IpcMessageType.GAME_FAILED, new { GameId = gameId, Error = "Game not registered" });
            throw new Exception($"Game with ID {gameId} is not registered.");
        }

        try
        {
            // Security: Basic path validation
            if (string.IsNullOrWhiteSpace(game.ExecutablePath))
            {
                 throw new Exception("Executable path is empty");
            }

            _logger.LogInformation("Launching game: {name} ({gameId}) with policy {policy}", game.Name, gameId, game.LaunchPolicy);

            string verb = game.LaunchPolicy == LaunchPolicy.Admin ? "runas" : "";
            var process = _processManager.StartProcess(game.ExecutablePath, game.Arguments, game.WorkingDirectory, verb);
            _trackedProcesses.TryAdd(process.Id, gameId);

            _ = NotifyIpcAsync(IpcMessageType.GAME_LAUNCHED, new ProcessEventPayload
            {
                Pid = process.Id,
                GameId = gameId,
                Name = game.Name
            });

            _ = NotifyIpcAsync(IpcMessageType.PROCESS_STARTED, new { Pid = process.Id, Name = game.Name });

            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) =>
            {
                if (sender is Process p)
                {
                    if (_trackedProcesses.TryRemove(p.Id, out var gId))
                    {
                        _logger.LogInformation("Game exited: {name} (PID: {pid})", game.Name, p.Id);
                        _ = NotifyIpcAsync(IpcMessageType.GAME_EXITED, new ProcessEventPayload
                        {
                            Pid = p.Id,
                            GameId = gId,
                            Name = game.Name
                        });
                        _ = NotifyIpcAsync(IpcMessageType.PROCESS_EXITED, new { Pid = p.Id, Name = game.Name });
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch game: {name}", game.Name);
            _ = NotifyIpcAsync(IpcMessageType.GAME_FAILED, new { GameId = gameId, Error = ex.Message });
            throw;
        }
    }

    public IEnumerable<ProcessEventPayload> GetRunningGames()
    {
        var running = new List<ProcessEventPayload>();
        foreach (var kvp in _trackedProcesses)
        {
            if (_gameRegistry.TryGetValue(kvp.Value, out var game))
            {
                running.Add(new ProcessEventPayload
                {
                    Pid = kvp.Key,
                    GameId = kvp.Value,
                    Name = game.Name
                });
            }
        }
        return running;
    }

    public void KillGame(string gameId)
    {
        var pids = _trackedProcesses.Where(kvp => kvp.Value == gameId).Select(kvp => kvp.Key).ToList();
        foreach (var pid in pids)
        {
            _processManager.KillProcess(pid);
        }
    }

    private async Task NotifyIpcAsync(IpcMessageType type, object? payload = null)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var ipcServer = scope.ServiceProvider.GetService<IpcServer>();
            if (ipcServer != null)
            {
                await ipcServer.BroadcastEventAsync(type, payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify IPC Server of game event.");
        }
    }
}
