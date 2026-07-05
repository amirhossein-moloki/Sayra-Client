using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Ipc;
using Sayra.Client.Shared.Models;
using SayraClient.Services;
using System.IO.Pipes;
using System.Text.Json;

namespace SayraClient.Services;

public class IpcServer : BackgroundService
{
    private const string PipeName = "SayraClientIpcPipe";
    private readonly ILogger<IpcServer> _logger;
    private readonly SessionManager _sessionManager;
    private readonly ClientStateManager _stateManager;
    private readonly KioskManager _kioskManager;
    private readonly ProcessManager _processManager;
    private readonly GameLauncher _gameLauncher;
    private readonly List<NamedPipeServerStream> _activeConnections = new();
    private readonly object _connectionsLock = new();

    public IpcServer(
        ILogger<IpcServer> logger,
        SessionManager sessionManager,
        ClientStateManager stateManager,
        KioskManager kioskManager,
        ProcessManager processManager,
        GameLauncher gameLauncher)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _stateManager = stateManager;
        _kioskManager = kioskManager;
        _processManager = processManager;
        _gameLauncher = gameLauncher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IPC Server starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var serverStream = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await serverStream.WaitForConnectionAsync(stoppingToken);
                _logger.LogInformation("UI Client connected to IPC.");

                lock (_connectionsLock)
                {
                    _activeConnections.Add(serverStream);
                }

                _ = HandleClientAsync(serverStream, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error accepting IPC connection.");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream) { AutoFlush = true };

        try
        {
            while (!ct.IsCancellationRequested && stream.IsConnected)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                var message = JsonSerializer.Deserialize<IpcMessage>(line);
                if (message != null)
                {
                    var responsePayload = await ProcessMessageAsync(message);
                    var response = new IpcMessage
                    {
                        RequestId = message.RequestId,
                        MessageType = IpcMessageType.COMMAND_RESPONSE,
                        Payload = JsonSerializer.Serialize(responsePayload)
                    };
                    await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                    await writer.FlushAsync();
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error handling IPC client.");
        }
        finally
        {
            lock (_connectionsLock)
            {
                _activeConnections.Remove(stream);
            }
            stream.Dispose();
            _logger.LogInformation("UI Client disconnected from IPC.");
        }
    }

    private async Task<IpcCommandResponse> ProcessMessageAsync(IpcMessage message)
    {
        _logger.LogDebug("Processing IPC message: {type}", message.MessageType);

        try
        {
            switch (message.MessageType)
            {
                case IpcMessageType.GET_STATE:
                    var state = GetCurrentStateDto();
                    return new IpcCommandResponse { Success = true, Result = JsonSerializer.Serialize(state) };

                case IpcMessageType.START_SESSION:
                    // This normally comes from server, but UI might trigger a local guest session if allowed
                    return new IpcCommandResponse { Success = false, ErrorMessage = "START_SESSION must be initiated by server." };

                case IpcMessageType.STOP_SESSION:
                    var stopResult = _sessionManager.StopSession("LOCAL_UI");
                    return new IpcCommandResponse { Success = stopResult.Status == "Executed", ErrorMessage = stopResult.Result };

                case IpcMessageType.PAUSE_SESSION:
                    var pauseResult = _sessionManager.PauseSession("LOCAL_UI");
                    return new IpcCommandResponse { Success = pauseResult.Status == "Executed", ErrorMessage = pauseResult.Result };

                case IpcMessageType.RESUME_SESSION:
                    var resumeResult = _sessionManager.ResumeSession("LOCAL_UI");
                    return new IpcCommandResponse { Success = resumeResult.Status == "Executed", ErrorMessage = resumeResult.Result };

                case IpcMessageType.LAUNCH_APP:
                    var launchReq = JsonSerializer.Deserialize<LaunchAppRequest>(message.Payload ?? "{}");
                    if (launchReq != null && !string.IsNullOrEmpty(launchReq.GameId))
                    {
                        _gameLauncher.LaunchGame(launchReq.GameId);
                        return new IpcCommandResponse { Success = true };
                    }
                    return new IpcCommandResponse { Success = false, ErrorMessage = "Invalid launch request: GameId is required." };

                case IpcMessageType.KILL_APP:
                    var killReq = JsonSerializer.Deserialize<KillAppRequest>(message.Payload ?? "{}");
                    if (killReq != null)
                    {
                        if (killReq.Pid.HasValue)
                        {
                            _processManager.KillProcess(killReq.Pid.Value);
                        }
                        else if (!string.IsNullOrEmpty(killReq.GameId))
                        {
                            _gameLauncher.KillGame(killReq.GameId);
                        }
                        else if (!string.IsNullOrEmpty(killReq.ProcessName))
                        {
                            _processManager.KillProcessByName(killReq.ProcessName);
                        }
                        else
                        {
                            return new IpcCommandResponse { Success = false, ErrorMessage = "Invalid kill request: Pid, GameId, or ProcessName required." };
                        }

                        await BroadcastEventAsync(IpcMessageType.PROCESS_KILLED, new { killReq.Pid, killReq.GameId, killReq.ProcessName });
                        return new IpcCommandResponse { Success = true };
                    }
                    return new IpcCommandResponse { Success = false, ErrorMessage = "Invalid kill request." };

                case IpcMessageType.LOCK_PC:
                    _kioskManager.Lockdown();
                    return new IpcCommandResponse { Success = true };

                case IpcMessageType.GET_APPS:
                    var registeredGames = _gameLauncher.GetRegisteredGames();
                    var appDtos = registeredGames.Select(g => new AppDto
                    {
                        Id = g.GameId,
                        Name = g.Name,
                        Category = g.Category,
                        IconPath = g.IconPath,
                        IsFavorite = g.IsFavorite
                    }).ToList();
                    return new IpcCommandResponse { Success = true, Result = JsonSerializer.Serialize(appDtos) };

                case IpcMessageType.GET_RUNNING_GAMES:
                    var runningGames = _gameLauncher.GetRunningGames();
                    return new IpcCommandResponse { Success = true, Result = JsonSerializer.Serialize(runningGames) };

                default:
                    return new IpcCommandResponse { Success = false, ErrorMessage = $"Unknown message type: {message.MessageType}" };
            }
        }
        catch (Exception ex)
        {
            return new IpcCommandResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    private ClientStateDto GetCurrentStateDto()
    {
        var session = _sessionManager.GetCurrentSession();
        return new ClientStateDto
        {
            CoreState = (ClientCoreState)_stateManager.CurrentState,
            SessionStatus = Enum.TryParse<SessionStatus>(session?.Status ?? "IDLE", out var status) ? status : SessionStatus.IDLE,
            RemainingTime = session != null ? TimeSpan.FromMinutes(session.Duration) - TimeSpan.FromSeconds(session.ElapsedSeconds) : TimeSpan.Zero,
            StartTime = session?.StartTime,
            ElapsedSeconds = session?.ElapsedSeconds ?? 0,
            TotalDurationMinutes = session?.Duration ?? 0,
            RatePerHour = session?.RatePerHour ?? 0,
            CurrentCost = session?.CurrentCost ?? 0,
            UserName = "User", // Mock for now
            IsKioskLocked = _kioskManager.IsLocked()
        };
    }

    public async Task BroadcastEventAsync(IpcMessageType type, object? payload = null)
    {
        var message = new IpcMessage
        {
            MessageType = type,
            Payload = payload != null ? JsonSerializer.Serialize(payload) : null
        };

        var json = JsonSerializer.Serialize(message);

        List<NamedPipeServerStream> targets;
        lock (_connectionsLock)
        {
            targets = _activeConnections.Where(s => s.IsConnected).ToList();
        }

        foreach (var stream in targets)
        {
            try
            {
                // We use the stream directly to avoid disposing issues with StreamWriter
                var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync(json);
                await writer.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send event to IPC client.");
            }
        }
    }
}
