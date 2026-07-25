using System;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Models
{
    public class LaunchRequest
    {
        public string GameId { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public Guid RuntimeSessionId { get; set; }
    }
}
