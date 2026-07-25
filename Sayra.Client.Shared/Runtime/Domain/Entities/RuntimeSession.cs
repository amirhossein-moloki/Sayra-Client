using System;
using Sayra.Client.Shared.Runtime.Domain.States;

namespace Sayra.Client.Shared.Runtime.Domain.Entities
{
    /// <summary>
    /// Represents a monitored game execution session.
    /// </summary>
    public class RuntimeSession
    {
        public Guid SessionId { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public string GameId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public RuntimeState Status { get; set; } = RuntimeState.Created;
        public RuntimeState RuntimeState { get; set; } = RuntimeState.Created;
    }
}
