using System;
using Sayra.Client.Shared.Runtime.Domain.Entities;

namespace Sayra.Client.Shared.Runtime.Domain.Events
{
    public class RuntimeSessionCreatedEvent
    {
        public RuntimeSession Session { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public RuntimeSessionCreatedEvent(RuntimeSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }
    }
}
