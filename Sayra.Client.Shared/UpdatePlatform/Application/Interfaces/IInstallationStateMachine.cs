using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Event arguments for installation state changes.
    /// </summary>
    public class InstallationStateChangedEventArgs : EventArgs
    {
        public InstallationState OldState { get; }
        public InstallationState NewState { get; }

        public InstallationStateChangedEventArgs(InstallationState oldState, InstallationState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// Represents a thread-safe deterministic finite state machine (FSM) managing installation states.
    /// </summary>
    public interface IInstallationStateMachine
    {
        /// <summary>
        /// Gets the current state of the installation.
        /// </summary>
        InstallationState CurrentState { get; }

        /// <summary>
        /// Attempts to transition to the target state. Throws an exception if transition is invalid.
        /// </summary>
        /// <param name="newState">The target state.</param>
        void TransitionTo(InstallationState newState);

        /// <summary>
        /// Checks whether transitioning to the target state is allowed from the current state.
        /// </summary>
        /// <param name="newState">The target state.</param>
        /// <returns>True if allowed; otherwise, false.</returns>
        bool CanTransitionTo(InstallationState newState);

        /// <summary>
        /// Occurs when the current state changes.
        /// </summary>
        event EventHandler<InstallationStateChangedEventArgs>? StateChanged;
    }
}
