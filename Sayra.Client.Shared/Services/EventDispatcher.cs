using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sayra.Client.Shared.Interfaces;

namespace Sayra.Client.Shared.Services
{
    public class EventDispatcher : IEventDispatcher
    {
        private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();

        public void Dispatch<T>(T @event)
        {
            if (@event == null) return;
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
                                // Avoid throwing during event dispatch to preserve robustness
                            }
                        }
                    }
                }
            }
        }

        public void RegisterHandler<T>(Action<T> handler)
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
