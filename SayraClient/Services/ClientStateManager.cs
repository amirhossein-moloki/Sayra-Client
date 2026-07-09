using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public enum ClientState
{
    STARTING,
    DISCOVERING,
    DISCOVERING_SERVER,
    CONNECTING,
    AUTHENTICATING,
    READY,
    IN_SESSION,
    DISCONNECTED,
    RECOVERING
}

public class ClientStateManager
{
    private readonly ILogger<ClientStateManager> _logger;
    private ClientState _currentState = ClientState.STARTING;
    private readonly object _stateLock = new();

    public event Action<ClientState, ClientState>? StateChanged;

    private readonly IServiceProvider _serviceProvider;

    public ClientStateManager(ILogger<ClientStateManager> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public ClientState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
    }

    public void TransitionTo(ClientState newState)
    {
        lock (_stateLock)
        {
            if (_currentState == newState) return;

            var oldState = _currentState;
            _currentState = newState;
            _logger.LogInformation("Client state transition: {oldState} -> {newState}", oldState, newState);
            StateChanged?.Invoke(oldState, newState);
            _ = NotifyIpcAsync();
        }
    }

    private async Task NotifyIpcAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var ipcServer = scope.ServiceProvider.GetService<IpcServer>();
            if (ipcServer != null)
            {
                await ipcServer.BroadcastEventAsync(Sayra.Client.Shared.Ipc.IpcMessageType.STATE_UPDATED);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify IPC Server of state change.");
        }
    }

    public bool IsReady()
    {
        var state = CurrentState;
        return state == ClientState.READY || state == ClientState.IN_SESSION;
    }
}
