using System;

namespace Sayra.Client.Shared.Runtime.Domain.Entities
{
    /// <summary>
    /// Context parameters for launching and managing a game process.
    /// </summary>
    public class GameRuntimeContext
    {
        public string GameIdentifier { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public int? ProcessId { get; set; }
        public Guid SessionId { get; set; }
        public string LaunchArguments { get; set; } = string.Empty;
    }
}
