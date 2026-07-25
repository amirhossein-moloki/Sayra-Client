using System;
using Sayra.Client.Shared.Runtime.Domain.States;

namespace Sayra.Client.Shared.Runtime.Application.Interfaces
{
    public interface IRuntimeStateManager
    {
        RuntimeState CurrentState { get; }
        void TransitionTo(RuntimeState newState, string? reason = null);
    }
}
