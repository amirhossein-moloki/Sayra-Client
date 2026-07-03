using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class GameLauncher
{
    private readonly ProcessManager _processManager;
    private readonly ILogger<GameLauncher> _logger;
    private readonly ConcurrentDictionary<int, string> _launchedProcesses = new();

    public GameLauncher(ProcessManager processManager, ILogger<GameLauncher> logger)
    {
        _processManager = processManager;
        _logger = logger;
    }

    public void LaunchGame(string path)
    {
        try
        {
            var process = _processManager.StartProcess(path);
            _launchedProcesses.TryAdd(process.Id, path);

            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) =>
            {
                if (sender is Process p)
                {
                    _launchedProcesses.TryRemove(p.Id, out _);
                    _logger.LogInformation("Game exited: {path} (PID: {pid})", path, p.Id);
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch game at {path}", path);
            throw;
        }
    }

    public IEnumerable<int> GetLaunchedProcessIds() => _launchedProcesses.Keys;
}
