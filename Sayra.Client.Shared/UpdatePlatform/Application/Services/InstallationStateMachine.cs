using System;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Thread-safe deterministic finite state machine (FSM) implementation for update installation states.
    /// </summary>
    public class InstallationStateMachine : IInstallationStateMachine
    {
        private readonly object _lock = new object();
        private InstallationState _currentState = InstallationState.Idle;

        /// <inheritdoc />
        public InstallationState CurrentState
        {
            get
            {
                lock (_lock)
                {
                    return _currentState;
                }
            }
        }

        /// <inheritdoc />
        public event EventHandler<InstallationStateChangedEventArgs>? StateChanged;

        /// <inheritdoc />
        public void TransitionTo(InstallationState newState)
        {
            InstallationState oldState;
            lock (_lock)
            {
                if (!CanTransitionToInternal(_currentState, newState))
                {
                    throw new InstallationFailedException($"Invalid state transition from {_currentState} to {newState}.");
                }

                oldState = _currentState;
                _currentState = newState;
            }

            StateChanged?.Invoke(this, new InstallationStateChangedEventArgs(oldState, newState));
        }

        /// <inheritdoc />
        public bool CanTransitionTo(InstallationState newState)
        {
            lock (_lock)
            {
                return CanTransitionToInternal(_currentState, newState);
            }
        }

        private bool CanTransitionToInternal(InstallationState current, InstallationState next)
        {
            if (next == InstallationState.Failed)
            {
                // Can always transition to Failed except from Completed/Failed
                return current != InstallationState.Completed && current != InstallationState.Failed;
            }

            switch (current)
            {
                case InstallationState.Idle:
                    return next == InstallationState.Preparing;

                case InstallationState.Preparing:
                    return next == InstallationState.Validating;

                case InstallationState.Validating:
                    return next == InstallationState.Staging;

                case InstallationState.Staging:
                    return next == InstallationState.StoppingServices;

                case InstallationState.StoppingServices:
                    return next == InstallationState.Installing;

                case InstallationState.Installing:
                    return next == InstallationState.Verifying;

                case InstallationState.Verifying:
                    return next == InstallationState.Restarting;

                case InstallationState.Restarting:
                    return next == InstallationState.Completed;

                case InstallationState.Completed:
                case InstallationState.Failed:
                    return false; // Terminal states

                default:
                    return false;
            }
        }
    }
}
