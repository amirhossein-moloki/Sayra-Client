using System;
using Microsoft.Extensions.Logging;

namespace Sayra.Client.Shared.Runtime.Overlay.Domain.States
{
    /// <summary>
    /// Thread-safe finite state machine enforcing valid transitions across the overlay lifecycle.
    /// </summary>
    public class OverlayStateMachine
    {
        private readonly ILogger _logger;
        private OverlayState _currentState = OverlayState.Hidden;
        private readonly object _lock = new();

        public OverlayState CurrentState
        {
            get
            {
                lock (_lock)
                {
                    return _currentState;
                }
            }
        }

        public OverlayStateMachine(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void TransitionTo(OverlayState newState)
        {
            lock (_lock)
            {
                if (_currentState == OverlayState.Disposed)
                {
                    throw new InvalidOperationException("Cannot transition from a Disposed state.");
                }

                if (!IsValidTransition(_currentState, newState))
                {
                    var errorMsg = $"Invalid state transition from {_currentState} to {newState}.";
                    _logger.LogError(errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }

                var oldState = _currentState;
                _currentState = newState;
                _logger.LogInformation("Overlay State Machine: {OldState} -> {NewState}", oldState, newState);
            }
        }

        private bool IsValidTransition(OverlayState from, OverlayState to)
        {
            if (from == to) return true;

            switch (from)
            {
                case OverlayState.Hidden:
                    return to == OverlayState.Initializing || to == OverlayState.Visible || to == OverlayState.Disposed;

                case OverlayState.Initializing:
                    return to == OverlayState.Visible || to == OverlayState.Hidden || to == OverlayState.Disposed;

                case OverlayState.Visible:
                    return to == OverlayState.Updating || to == OverlayState.Closing || to == OverlayState.Hidden || to == OverlayState.Disposed;

                case OverlayState.Updating:
                    return to == OverlayState.Visible || to == OverlayState.Closing || to == OverlayState.Hidden || to == OverlayState.Disposed;

                case OverlayState.Closing:
                    return to == OverlayState.Hidden || to == OverlayState.Disposed;

                case OverlayState.Disposed:
                    return false;

                default:
                    return false;
            }
        }
    }
}
