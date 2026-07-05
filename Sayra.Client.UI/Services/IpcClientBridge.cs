using Sayra.Client.Shared.Ipc;
using Sayra.Client.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

namespace Sayra.Client.UI.Services;

public class IpcClientBridge : IClientBridge, IDisposable
{
    private const string PipeName = "SayraClientIpcPipe";
    private readonly Subject<ClientState> _stateSubject = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<IpcMessage>> _pendingRequests = new();
    private NamedPipeClientStream? _clientStream;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private ClientStatus _currentStatus = ClientStatus.Disconnected;

    public IpcClientBridge()
    {
        StartConnectionLoop();
    }

    private void StartConnectionLoop()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ConnectLoopAsync(_cts.Token));
    }

    private async Task ConnectLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                UpdateStatus(ClientStatus.Connecting);
                _clientStream = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await _clientStream.ConnectAsync(5000, ct);

                _writer = new StreamWriter(_clientStream) { AutoFlush = true };
                var reader = new StreamReader(_clientStream);

                UpdateStatus(ClientStatus.Connected);
                _logger_info("Connected to Core IPC.");

                // Start listening for messages
                while (!ct.IsCancellationRequested && _clientStream.IsConnected)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line == null) break;

                    var message = JsonSerializer.Deserialize<IpcMessage>(line);
                    if (message != null)
                    {
                        HandleIncomingMessage(message);
                    }
                }
            }
            catch (Exception)
            {
                // Failed to connect or disconnected
                UpdateStatus(ClientStatus.Disconnected);
            }
            finally
            {
                CleanupConnection();
            }

            if (!ct.IsCancellationRequested)
            {
                await Task.Delay(2000, ct); // Wait before reconnect
            }
        }
    }

    private void UpdateStatus(ClientStatus status)
    {
        _currentStatus = status;
        NotifyStateChanged();
    }

    private void HandleIncomingMessage(IpcMessage message)
    {
        if (message.MessageType == IpcMessageType.COMMAND_RESPONSE)
        {
            if (_pendingRequests.TryRemove(message.RequestId, out var tcs))
            {
                tcs.SetResult(message);
            }
        }
        else
        {
            // Event from core
            switch (message.MessageType)
            {
                case IpcMessageType.STATE_UPDATED:
                case IpcMessageType.SESSION_TIME_UPDATED:
                    NotifyStateChanged();
                    break;
            }
        }
    }

    private void NotifyStateChanged()
    {
        // For now, we'll fetch the full state whenever something changes
        _ = GetState().ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                _stateSubject.OnNext(t.Result);
            }
        });
    }

    private void CleanupConnection()
    {
        _writer?.Dispose();
        _writer = null;
        _clientStream?.Dispose();
        _clientStream = null;

        // Cancel all pending requests
        foreach (var key in _pendingRequests.Keys)
        {
            if (_pendingRequests.TryRemove(key, out var tcs))
            {
                tcs.TrySetCanceled();
            }
        }
    }

    public async Task<ClientState> GetState()
    {
        if (_currentStatus != ClientStatus.Connected)
        {
            return new ClientState { Status = _currentStatus };
        }

        try
        {
            var response = await SendRequestAsync(IpcMessageType.GET_STATE);
            var stateDto = JsonSerializer.Deserialize<ClientStateDto>(response.Payload ?? "{}");

            return new ClientState
            {
                Status = _currentStatus,
                SessionState = MapSessionState(stateDto?.SessionStatus ?? SessionStatus.IDLE),
                RemainingTime = stateDto?.RemainingTime ?? TimeSpan.Zero,
                UserName = stateDto?.UserName
            };
        }
        catch
        {
            return new ClientState { Status = _currentStatus };
        }
    }

    private SessionState MapSessionState(SessionStatus status)
    {
        return status switch
        {
            SessionStatus.IDLE => SessionState.Idle,
            SessionStatus.ACTIVE => SessionState.Active,
            SessionStatus.PAUSED => SessionState.Paused,
            SessionStatus.ENDED => SessionState.Ended,
            _ => SessionState.Idle
        };
    }

    public async Task SendCommand(string action, object? parameters = null)
    {
        IpcMessageType type = action switch
        {
            "STOP_SESSION" => IpcMessageType.STOP_SESSION,
            "PAUSE_SESSION" => IpcMessageType.PAUSE_SESSION,
            "RESUME_SESSION" => IpcMessageType.RESUME_SESSION,
            "RUN_APP" => IpcMessageType.LAUNCH_APP,
            "KILL_APP" => IpcMessageType.KILL_APP,
            "LOCK_PC" => IpcMessageType.LOCK_PC,
            "START_SESSION" => IpcMessageType.START_SESSION,
            _ => throw new ArgumentException($"Unknown action: {action}")
        };

        string? payload = parameters != null ? JsonSerializer.Serialize(parameters) : null;
        var response = await SendRequestAsync(type, payload);
        var commandResponse = JsonSerializer.Deserialize<IpcCommandResponse>(response.Payload ?? "{}");

        if (commandResponse != null && !commandResponse.Success)
        {
            throw new Exception(commandResponse.ErrorMessage ?? "Command failed");
        }
    }

    public IObservable<ClientState> SubscribeToStateChanged() => _stateSubject.AsObservable();

    public async Task<IEnumerable<AppModel>> GetApplications()
    {
        try
        {
            var response = await SendRequestAsync(IpcMessageType.GET_APPS);
            var appDtos = JsonSerializer.Deserialize<List<AppDto>>(response.ResultFromPayload() ?? "[]");

            return appDtos?.Select(a => new AppModel
            {
                Id = a.Id,
                Name = a.Name,
                Category = a.Category,
                Description = a.Description,
                IconPath = a.IconPath,
                IsFavorite = a.IsFavorite
            }) ?? Enumerable.Empty<AppModel>();
        }
        catch
        {
            return Enumerable.Empty<AppModel>();
        }
    }

    private async Task<IpcMessage> SendRequestAsync(IpcMessageType type, string? payload = null)
    {
        if (_writer == null || _clientStream == null || !_clientStream.IsConnected)
        {
            throw new InvalidOperationException("Not connected to Core IPC.");
        }

        var message = new IpcMessage
        {
            MessageType = type,
            Payload = payload
        };

        var tcs = new TaskCompletionSource<IpcMessage>();
        _pendingRequests[message.RequestId] = tcs;

        await _writer.WriteLineAsync(JsonSerializer.Serialize(message));

        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private void _logger_info(string msg) => System.Diagnostics.Debug.WriteLine(msg);

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        CleanupConnection();
        _stateSubject.Dispose();
    }
}

public static class IpcExtensions
{
    public static string? ResultFromPayload(this IpcMessage message)
    {
        if (string.IsNullOrEmpty(message.Payload)) return null;
        var response = JsonSerializer.Deserialize<IpcCommandResponse>(message.Payload);
        return response?.Result;
    }
}
