using System;

namespace Sayra.Client.Shared.Runtime.Domain.Entities
{
    /// <summary>
    /// Represents a command sent to the runtime control subsystem.
    /// </summary>
    public class RuntimeCommand
    {
        public Guid CommandId { get; set; } = Guid.NewGuid();
        public string CommandType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
