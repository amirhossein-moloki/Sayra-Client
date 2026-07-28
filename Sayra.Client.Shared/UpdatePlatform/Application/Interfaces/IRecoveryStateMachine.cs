using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    public class RecoveryStateChangedEventArgs : EventArgs
    {
        public RecoveryState OldState { get; }
        public RecoveryState NewState { get; }

        public RecoveryStateChangedEventArgs(RecoveryState oldState, RecoveryState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// Represents a thread-safe finite state machine orchestrating recovery phases.
    /// </summary>
    public interface IRecoveryStateMachine
    {
        /// <summary>
        /// Gets the current state of the recovery machine.
        /// </summary>
        RecoveryState CurrentState { get; }

        /// <summary>
        /// Transitions to the target state if the transition is valid and thread-safe.
        /// </summary>
        void TransitionTo(RecoveryState newState);

        /// <summary>
        /// Raised when the recovery state changes.
        /// </summary>
        event EventHandler<RecoveryStateChangedEventArgs> StateChanged;
    }
}
