using System;

namespace Sayra.Client.Shared.Runtime.Domain.States
{
    /// <summary>
    /// Represents the finite states of the game execution runtime.
    /// </summary>
    public enum RuntimeState
    {
        Created,
        Preparing,
        Starting,
        Running,
        Paused,
        Stopping,
        Completed,
        Failed
    }
}
