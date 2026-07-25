using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Runtime.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.Application.Services
{
    public class RuntimeEventPublisher : IRuntimeEventPublisher
    {
        private readonly IEventDispatcher? _eventDispatcher;
        private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();

        public RuntimeEventPublisher(IEventDispatcher? eventDispatcher = null)
        {
            _eventDispatcher = eventDispatcher;
        }

        public void Publish<T>(T @event) where T : class
        {
            if (@event == null) return;

            // Publish through external event dispatcher if registered in DI
            _eventDispatcher?.Dispatch(@event);

            // Also publish through local subscriptions
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var handlers))
            {
                lock (handlers)
                {
                    foreach (var handler in handlers)
                    {
                        if (handler is Action<T> action)
                        {
                            try
                            {
                                action(@event);
                            }
                            catch
                            {
                                // Robutness safeguard: avoid throwing in event dispatch to keep process stable
                            }
                        }
                    }
                }
            }
        }

        public void Subscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null) return;
            var type = typeof(T);
            _handlers.AddOrUpdate(type,
                _ => new List<object> { handler },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(handler);
                    }
                    return list;
                });
        }
    }
}
