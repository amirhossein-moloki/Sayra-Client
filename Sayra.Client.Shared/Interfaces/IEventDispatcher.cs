using System;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IEventDispatcher
    {
        void Dispatch<T>(T @event);
        void RegisterHandler<T>(Action<T> handler);
    }
}
