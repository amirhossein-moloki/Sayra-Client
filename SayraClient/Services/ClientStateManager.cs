using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public enum ClientState
{
    STARTING,
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

    public ClientStateManager(ILogger<ClientStateManager> logger)
    {
        _logger = logger;
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
        }
    }

    public bool IsReady()
    {
        var state = CurrentState;
        return state == ClientState.READY || state == ClientState.IN_SESSION;
    }
}
