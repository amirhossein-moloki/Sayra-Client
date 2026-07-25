using System;
using Sayra.Client.Shared.Runtime.Launch.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Events
{
    public class LaunchRequestedEvent
    {
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string GameId { get; }
        public Guid SessionId { get; }
        public LaunchRequest Request { get; }

        public LaunchRequestedEvent(LaunchRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            GameId = request.GameId;
            SessionId = request.RuntimeSessionId;
        }
    }
}
