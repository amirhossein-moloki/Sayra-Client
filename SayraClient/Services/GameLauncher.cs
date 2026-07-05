using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class GameLauncher
{
    private readonly ProcessManager _processManager;
    private readonly ILogger<GameLauncher> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<int, string> _launchedProcesses = new();

    public GameLauncher(ProcessManager processManager, ILogger<GameLauncher> logger, IServiceProvider serviceProvider)
    {
        _processManager = processManager;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public void LaunchGame(string path)
    {
        try
        {
            var process = _processManager.StartProcess(path);
            _launchedProcesses.TryAdd(process.Id, path);
            _ = NotifyIpcAsync(Sayra.Client.Shared.Ipc.IpcMessageType.PROCESS_STARTED, new { Pid = process.Id, Path = path });

            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) =>
            {
                if (sender is Process p)
                {
                    _launchedProcesses.TryRemove(p.Id, out _);
                    _logger.LogInformation("Game exited: {path} (PID: {pid})", path, p.Id);
                    _ = NotifyIpcAsync(Sayra.Client.Shared.Ipc.IpcMessageType.PROCESS_EXITED, new { Pid = p.Id, Path = path });
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

    private async Task NotifyIpcAsync(Sayra.Client.Shared.Ipc.IpcMessageType type, object? payload = null)
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
            _logger.LogWarning(ex, "Failed to notify IPC Server of process event.");
        }
    }
}
