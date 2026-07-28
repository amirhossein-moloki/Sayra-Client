using System;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Thread-safe implementation of the Recovery State Machine.
    /// </summary>
    public class RecoveryStateMachine : IRecoveryStateMachine
    {
        private readonly object _lock = new object();
        private RecoveryState _currentState = RecoveryState.Idle;

        public RecoveryState CurrentState
        {
            get
            {
                lock (_lock)
                {
                    return _currentState;
                }
            }
        }

        public event EventHandler<RecoveryStateChangedEventArgs> StateChanged;

        public void TransitionTo(RecoveryState newState)
        {
            RecoveryState oldState;
            lock (_lock)
            {
                if (!IsValidTransition(_currentState, newState))
                {
                    throw new RecoveryFailedException($"Invalid state transition from {_currentState} to {newState}.");
                }

                oldState = _currentState;
                _currentState = newState;
            }

            StateChanged?.Invoke(this, new RecoveryStateChangedEventArgs(oldState, newState));
        }

        private bool IsValidTransition(RecoveryState from, RecoveryState to)
        {
            if (to == RecoveryState.Failed)
            {
                // Can fail from anywhere except Completed or Failed
                return from != RecoveryState.Completed && from != RecoveryState.Failed;
            }

            switch (from)
            {
                case RecoveryState.Idle:
                    return to == RecoveryState.BackupCreated || to == RecoveryState.RecoveryRequired || to == RecoveryState.Monitoring;

                case RecoveryState.BackupCreated:
                    return to == RecoveryState.Monitoring || to == RecoveryState.RecoveryRequired;

                case RecoveryState.Monitoring:
                    return to == RecoveryState.RecoveryRequired || to == RecoveryState.Completed;

                case RecoveryState.RecoveryRequired:
                    return to == RecoveryState.RollingBack || to == RecoveryState.Restoring;

                case RecoveryState.RollingBack:
                    return to == RecoveryState.Restoring || to == RecoveryState.Verifying;

                case RecoveryState.Restoring:
                    return to == RecoveryState.Verifying;

                case RecoveryState.Verifying:
                    return to == RecoveryState.Completed;

                case RecoveryState.Completed:
                case RecoveryState.Failed:
                default:
                    return false;
            }
        }
    }
}
