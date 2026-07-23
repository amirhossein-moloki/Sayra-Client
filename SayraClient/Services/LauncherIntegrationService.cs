using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Launcher.Events;
using Sayra.Client.Launcher.Services;
using Sayra.Client.Shared.Ipc;
using Sayra.Client.Shared.Models;
using Sayra.Client.OfflineQueue;
using Sayra.Client.OfflineQueue.Models;

namespace SayraClient.Services;

public class LauncherIntegrationService : IModule
{
    private readonly IGameLauncherService _launcherService;
    private readonly IProcessMonitorService _processMonitor;
    private readonly SessionManager _sessionManager;
    private readonly IpcServer _ipcServer;
    private readonly TcpClientManager _tcpClientManager;
    private readonly IOfflineQueueManager _queueManager;
    private readonly ILogger<LauncherIntegrationService> _logger;

    public string Name => "LauncherIntegrationModule";
    public IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

    public LauncherIntegrationService(
        IGameLauncherService launcherService,
        IProcessMonitorService processMonitor,
        SessionManager sessionManager,
        IpcServer ipcServer,
        TcpClientManager tcpClientManager,
        IOfflineQueueManager queueManager,
        ILogger<LauncherIntegrationService> logger)
    {
        _launcherService = launcherService;
        _processMonitor = processMonitor;
        _sessionManager = sessionManager;
        _ipcServer = ipcServer;
        _tcpClientManager = tcpClientManager;
        _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Launcher Integration Module initialized.");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Launcher Integration Module starting...");

        _launcherService.GameLaunching += OnGameLaunching;
        _launcherService.GameStarted += OnGameStarted;
        _launcherService.GameExited += OnGameExited;
        _launcherService.GameCrashed += OnGameCrashed;
        _launcherService.GameRestarted += OnGameRestarted;
        _launcherService.GameKilled += OnGameKilled;
        _launcherService.LaunchFailed += OnLaunchFailed;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Launcher Integration Module stopping...");

        _launcherService.GameLaunching -= OnGameLaunching;
        _launcherService.GameStarted -= OnGameStarted;
        _launcherService.GameExited -= OnGameExited;
        _launcherService.GameCrashed -= OnGameCrashed;
        _launcherService.GameRestarted -= OnGameRestarted;
        _launcherService.GameKilled -= OnGameKilled;
        _launcherService.LaunchFailed -= OnLaunchFailed;

        return Task.CompletedTask;
    }

    private void OnGameLaunching(object? sender, GameLaunchingEventArgs e)
    {
        _logger.LogInformation("[EVENT BUS] Game launching: '{Name}' (Id: {Id})", e.Name, e.GameId);
        _ = _ipcServer.BroadcastEventAsync(IpcMessageType.LAUNCH_GAME, new { GameId = e.GameId, Name = e.Name });
    }

    private void OnGameStarted(object? sender, GameStartedEventArgs e)
    {
        _logger.LogInformation("[EVENT BUS] Game started: '{Name}' (PID: {Pid}, Id: {Id})", e.Name, e.Pid, e.GameId);

        // Broadcast IPC Events
        _ = _ipcServer.BroadcastEventAsync(IpcMessageType.GAME_LAUNCHED, new ProcessEventPayload
        {
            Pid = e.Pid,
            GameId = e.GameId,
            Name = e.Name
        });
        _ = _ipcServer.BroadcastEventAsync(IpcMessageType.PROCESS_STARTED, new { Pid = e.Pid, Name = e.Name });

        // Notify Server
        var session = _sessionManager.GetCurrentSession();
        _ = SendServerEventAsync("GAME_STARTED", e.GameId, e.Name, $"PID: {e.Pid}, SessionId: {session?.SessionId}");
    }

    private void OnGameExited(object? sender, GameExitedEventArgs e)
    {
        _logger.LogInformation("[EVENT BUS] Game exited: '{Name}' (Id: {Id}, ExitCode: {ExitCode}, Duration: {Duration:hh\\:mm\\:ss})",
            e.Name, e.GameId, e.ExitCode, e.Duration);

        // Broadcast IPC Events
        _ = _ipcServer.BroadcastEventAsync(IpcMessageType.GAME_EXITED, new ProcessEventPayload
        {
            Pid = 0,
            GameId = e.GameId,
            Name = e.Name
        });
        _ = _ipcServer.BroadcastEventAsync(IpcMessageType.PROCESS_EXITED, new { Name = e.Name, ExitCode = e.ExitCode });

        // Notify Server
        _ = SendServerEventAsync("GAME_CLOSED", e.GameId, e.Name, $"Duration: {e.Duration.TotalSeconds} seconds, ExitCode: {e.ExitCode}");
    }

    private void OnGameCrashed(object? sender, GameCrashedEventArgs e)
    {
        _logger.LogError("[EVENT BUS] Game crashed: '{Name}' (Id: {Id}, ExitCode: {ExitCode}, Reason: '{Reason}')",
            e.Name, e.GameId, e.ExitCode, e.Reason);

        // Broadcast IPC Events
        _ = _ipcServer.BroadcastEventAsync(IpcMessageType.GAME_FAILED, new { GameId = e.GameId, Name = e.Name, Error = e.Reason });
        _ = _ipcServer.BroadcastEventAsync(IpcMessageType.SECURITY_BREACH_DETECTED, new SecurityEventPayload
        {
            EventType = "GAME_CRASH",
            Severity = "Medium",
            Description = $"Game {e.Name} crashed unexpectedly.",
            Details = $"ExitCode: {e.ExitCode}, Reason: {e.Reason}"
        });

        // Notify Server
        _ = SendServerEventAsync("GAME_CRASHED", e.GameId, e.Name, $"ExitCode: {e.ExitCode}, Reason: {e.Reason}");
    }

    private void OnGameRestarted(object? sender, GameRestartedEventArgs e)
    {
        _logger.LogWarning("[EVENT BUS] Game restarting: '{Name}' (Id: {Id}, Attempt: {Attempt})", e.Name, e.GameId, e.RetryCount);
        _processMonitor.IncrementRestarts();
        _ = _ipcServer.BroadcastEventAsync(IpcMessageType.RESTART_GAME, new { GameId = e.GameId, Name = e.Name, RetryCount = e.RetryCount });
    }

    private void OnGameKilled(object? sender, GameKilledEventArgs e)
    {
        _logger.LogWarning("[EVENT BUS] Game killed: '{Name}' (PID: {Pid}, Id: {Id})", e.Name, e.Pid, e.GameId);
        _ = _ipcServer.BroadcastEventAsync(IpcMessageType.PROCESS_KILLED, new { Pid = e.Pid, Name = e.Name });
    }

    private void OnLaunchFailed(object? sender, LaunchFailedEventArgs e)
    {
        _logger.LogError("[EVENT BUS] Launch failed: '{Name}' (Id: {Id}, Reason: '{Reason}')", e.Name, e.GameId, e.Reason);
        _ = _ipcServer.BroadcastEventAsync(IpcMessageType.GAME_FAILED, new { GameId = e.GameId, Name = e.Name, Error = e.Reason });
    }

    private async Task SendServerEventAsync(string eventType, string gameId, string name, string details)
    {
        try
        {
            var session = _sessionManager.GetCurrentSession();
            var payload = new
            {
                type = "EVENT",
                @event = eventType,
                timestamp = DateTime.UtcNow,
                pcId = session?.PcId ?? "UnknownPC",
                gameId = gameId,
                name = name,
                details = details
            };

            var clientEvent = new ClientEvent
            {
                EventType = eventType,
                Priority = eventType == "GAME_CRASHED" ? QueuePriority.HIGH : QueuePriority.NORMAL,
                ClientId = session?.PcId ?? "UnknownPC",
                SessionId = session?.SessionId ?? string.Empty,
                Payload = System.Text.Json.JsonSerializer.Serialize(payload)
            };

            _logger.LogInformation("Enqueuing game event {Type} to offline queue.", eventType);
            await _queueManager.AddEventAsync(clientEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue event {Type} to offline queue.", eventType);
        }
    }
}
