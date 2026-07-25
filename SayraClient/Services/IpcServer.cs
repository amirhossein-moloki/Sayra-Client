using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.GameLibrary.Services;
using Sayra.Client.Launcher.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Ipc;
using Sayra.Client.Shared.Models;

namespace SayraClient.Services;

public class IpcServer : SupervisedBackgroundService
{
    private const string PipeName = "SayraClientIpcPipe";
    private readonly SessionManager _sessionManager;
    private readonly ClientStateManager _stateManager;
    private readonly IKioskSecurityService _kioskManager;
    private readonly IGameLauncherService _gameLauncher;
    private readonly IProcessMonitorService _processMonitor;
    private readonly IGameLibraryService _gameLibrary;
    private readonly ISecureIpcPolicyManager _ipcPolicyManager;
    private readonly IAuditLogger _auditLogger;
    private readonly List<NamedPipeServerStream> _activeConnections = new();
    private readonly object _connectionsLock = new();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeClientProcessId(IntPtr hPipe, out int ClientProcessId);

    public IpcServer(
        ILogger<IpcServer> logger,
        SessionManager sessionManager,
        ClientStateManager stateManager,
        IKioskSecurityService kioskManager,
        IGameLauncherService gameLauncher,
        IProcessMonitorService processMonitor,
        IGameLibraryService gameLibrary,
        ISecureIpcPolicyManager ipcPolicyManager,
        IServiceHealthMonitor healthMonitor,
        IAuditLogger auditLogger)
        : base(logger, healthMonitor, "IpcServer")
    {
        _sessionManager = sessionManager;
        _stateManager = stateManager;
        _kioskManager = kioskManager;
        _gameLauncher = gameLauncher;
        _processMonitor = processMonitor;
        _gameLibrary = gameLibrary;
        _ipcPolicyManager = ipcPolicyManager;
        _auditLogger = auditLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IPC Server starting...");

        // Start a task to periodically report heartbeats
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _healthMonitor.ReportHeartbeat("IpcServer");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var serverStream = CreateNamedPipeServerStream();

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

    private NamedPipeServerStream CreateNamedPipeServerStream()
    {
        if (OperatingSystem.IsWindows())
        {
            var pipeSecurity = _ipcPolicyManager.GetSecurePipeSecurity();

            return System.IO.Pipes.NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                0, // inBufferSize
                0, // outBufferSize
                pipeSecurity);
        }
        else
        {
            return new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        var streamId = Guid.NewGuid().ToString();
        int callerPid = 0;

        if (OperatingSystem.IsWindows())
        {
            // 1. Validate Caller Windows Identity (DACL-level alternative identity verification)
            string clientSid = "";
            string clientName = "";
            bool isClientAuthorized = _ipcPolicyManager.ValidateIdentity(stream, out clientName, out clientSid);

            if (!isClientAuthorized)
            {
                _logger.LogWarning("REJECTED IPC Named Pipe connection. Unauthorized Windows Identity. Name: {Name}, SID: {Sid}", clientName, clientSid);
                _ipcPolicyManager.RemoveStream(streamId);
                stream.Dispose();
                return;
            }

            // 2. Retrieve Client Process ID
            if (GetNamedPipeClientProcessId(stream.SafePipeHandle.DangerousGetHandle(), out int pid))
            {
                callerPid = pid;
            }

            // 3. Validate Session, SID, and Process Identity by PID
            if (!_ipcPolicyManager.ValidateClient(callerPid))
            {
                _logger.LogWarning("REJECTED IPC Named Pipe connection. PID {Pid} failed process/session/SID verification.", callerPid);
                _ipcPolicyManager.RemoveStream(streamId);
                stream.Dispose();
                return;
            }

            _logger.LogInformation("AUTHORIZED IPC connection from client: {Name} (SID: {Sid}), PID {Pid}", clientName, clientSid, callerPid);
        }
        else
        {
            _logger.LogInformation("Authorized non-Windows connection with Stream ID {Id}", streamId);
        }

        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream) { AutoFlush = true };

        try
        {
            while (!ct.IsCancellationRequested && stream.IsConnected)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                // Protect against DoS with oversized messages
                if (line.Length > 65536)
                {
                    _logger.LogWarning("Dropped incoming line due to oversized length: {Length} characters.", line.Length);
                    _auditLogger.LogSecurity("IPC message rejected. Input line length exceeded safe limit of 64KB.", new Dictionary<string, object> { { "Length", line.Length } });
                    break;
                }

                IpcMessage? message = null;
                try
                {
                    message = JsonSerializer.Deserialize<IpcMessage>(line);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Malformed JSON received over IPC.");
                    var response = new IpcMessage
                    {
                        MessageType = IpcMessageType.COMMAND_RESPONSE,
                        Payload = JsonSerializer.Serialize(new IpcCommandResponse
                        {
                            Success = false,
                            ErrorMessage = "Malformed IPC request structure."
                        })
                    };
                    await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                    await writer.FlushAsync();
                    continue;
                }

                if (message != null)
                {
                    // Secure Connection Handshake: The client MUST successfully complete HANDSHAKE first
                    if (!_ipcPolicyManager.IsHandshaken(streamId))
                    {
                        bool handshakeSuccess = _ipcPolicyManager.ProcessHandshake(streamId, message, callerPid, out string handshakeError);
                        if (!handshakeSuccess)
                        {
                            // Return generic error to prevent leaking security policy internals
                            var response = new IpcMessage
                            {
                                RequestId = message.RequestId,
                                MessageType = IpcMessageType.COMMAND_RESPONSE,
                                Payload = JsonSerializer.Serialize(new IpcCommandResponse
                                {
                                    Success = false,
                                    ErrorMessage = "Access denied. Handshake validation failed."
                                })
                            };
                            await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                            await writer.FlushAsync();
                            break; // Immediately terminate connection on failed handshake!
                        }
                        else
                        {
                            var response = new IpcMessage
                            {
                                RequestId = message.RequestId,
                                MessageType = IpcMessageType.COMMAND_RESPONSE,
                                Payload = JsonSerializer.Serialize(new IpcCommandResponse
                                {
                                    Success = true,
                                    Result = "Handshake authorized."
                                })
                            };
                            await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                            await writer.FlushAsync();
                            continue;
                        }
                    }

                    // For all subsequent messages, enforce message-by-message security validation (Replay/Skew/Size)
                    if (!_ipcPolicyManager.ValidateMessage(message))
                    {
                        var response = new IpcMessage
                        {
                            RequestId = message.RequestId,
                            MessageType = IpcMessageType.COMMAND_RESPONSE,
                            Payload = JsonSerializer.Serialize(new IpcCommandResponse
                            {
                                Success = false,
                                ErrorMessage = "Request validation failed. Message rejected."
                            })
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                        await writer.FlushAsync();
                        continue;
                    }

                    var responsePayload = await ProcessMessageAsync(message);
                    var responseMsg = new IpcMessage
                    {
                        RequestId = message.RequestId,
                        MessageType = IpcMessageType.COMMAND_RESPONSE,
                        Payload = JsonSerializer.Serialize(responsePayload)
                    };
                    await writer.WriteLineAsync(JsonSerializer.Serialize(responseMsg));
                    await writer.FlushAsync();
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error handling IPC client stream.");
        }
        finally
        {
            _ipcPolicyManager.RemoveStream(streamId);
            lock (_connectionsLock)
            {
                _activeConnections.Remove(stream);
            }
            stream.Dispose();
            _logger.LogInformation("UI Client disconnected from IPC. Stream ID {Id} cleaned up.", streamId);
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
                case IpcMessageType.LAUNCH_GAME:
                    var launchReq = JsonSerializer.Deserialize<LaunchAppRequest>(message.Payload ?? "{}");
                    if (launchReq != null && !string.IsNullOrEmpty(launchReq.GameId))
                    {
                        bool success = await _gameLauncher.LaunchGameAsync(launchReq.GameId);
                        return new IpcCommandResponse { Success = success, ErrorMessage = success ? null : "Launch pipeline validation or startup failed." };
                    }
                    return new IpcCommandResponse { Success = false, ErrorMessage = "Invalid launch request: GameId is required." };

                case IpcMessageType.STOP_GAME:
                    var stopReq = JsonSerializer.Deserialize<LaunchAppRequest>(message.Payload ?? "{}");
                    if (stopReq != null && !string.IsNullOrEmpty(stopReq.GameId))
                    {
                        await _gameLauncher.StopGameAsync(stopReq.GameId);
                        return new IpcCommandResponse { Success = true };
                    }
                    return new IpcCommandResponse { Success = false, ErrorMessage = "Invalid stop request: GameId is required." };

                case IpcMessageType.RESTART_GAME:
                    var restartReq = JsonSerializer.Deserialize<LaunchAppRequest>(message.Payload ?? "{}");
                    if (restartReq != null && !string.IsNullOrEmpty(restartReq.GameId))
                    {
                        await _gameLauncher.RestartGameAsync(restartReq.GameId);
                        return new IpcCommandResponse { Success = true };
                    }
                    return new IpcCommandResponse { Success = false, ErrorMessage = "Invalid restart request: GameId is required." };

                case IpcMessageType.KILL_APP:
                    var killReq = JsonSerializer.Deserialize<KillAppRequest>(message.Payload ?? "{}");
                    if (killReq != null)
                    {
                        if (killReq.Pid.HasValue)
                        {
                            await _gameLauncher.KillProcessAsync(killReq.Pid.Value);
                        }
                        else if (!string.IsNullOrEmpty(killReq.GameId))
                        {
                            await _gameLauncher.KillGameAsync(killReq.GameId);
                        }
                        else if (!string.IsNullOrEmpty(killReq.ProcessName))
                        {
                            await _gameLauncher.KillProcessByNameAsync(killReq.ProcessName);
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
                    var registeredGames = await _gameLibrary.GetGames();
                    var appDtos = registeredGames.Select(g => new AppDto
                    {
                        Id = g.Id,
                        Name = g.Name,
                        Category = g.Category?.Name ?? "Game",
                        IconPath = g.IconPath,
                        IsFavorite = false
                    }).ToList();
                    return new IpcCommandResponse { Success = true, Result = JsonSerializer.Serialize(appDtos) };

                case IpcMessageType.GET_RUNNING_GAMES:
                    var runningGames = _gameLauncher.GetRunningGames().Select(g => new ProcessEventPayload
                    {
                        Pid = g.Pid,
                        GameId = g.GameId,
                        Name = g.Name
                    }).ToList();
                    return new IpcCommandResponse { Success = true, Result = JsonSerializer.Serialize(runningGames) };

                case IpcMessageType.GET_RUNNING_STATISTICS:
                    var statsList = _processMonitor.GetRunningProcesses();
                    return new IpcCommandResponse { Success = true, Result = JsonSerializer.Serialize(statsList) };

                case IpcMessageType.VALIDATE_EXECUTABLE:
                    var pathPayload = JsonSerializer.Deserialize<Dictionary<string, string>>(message.Payload ?? "{}");
                    if (pathPayload != null && pathPayload.TryGetValue("path", out var execPath))
                    {
                        bool exists = File.Exists(execPath);
                        return new IpcCommandResponse { Success = exists, Result = exists ? "Valid" : "File not found" };
                    }
                    return new IpcCommandResponse { Success = false, ErrorMessage = "Path payload is missing." };

                case IpcMessageType.LAUNCHER_STATUS:
                    var statusObj = new
                    {
                        Launches = _processMonitor.GetTotalLaunches(),
                        Crashes = _processMonitor.GetTotalCrashes(),
                        Restarts = _processMonitor.GetTotalRestarts()
                    };
                    return new IpcCommandResponse { Success = true, Result = JsonSerializer.Serialize(statusObj) };

                default:
                    return new IpcCommandResponse { Success = false, ErrorMessage = "Unknown request type." };
            }
        }
        catch (Exception ex)
        {
            // Never expose internal system details
            _logger.LogError(ex, "Internal error processing IPC message.");
            return new IpcCommandResponse { Success = false, ErrorMessage = "An internal server error occurred." };
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
