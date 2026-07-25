using System;

namespace Sayra.Client.Shared.Runtime.Application.Interfaces
{
    public interface IRuntimeEventPublisher
    {
        void Publish<T>(T @event) where T : class;
        void Subscribe<T>(Action<T> handler) where T : class;
    }
}
