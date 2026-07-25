using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Models
{
    public class LaunchProfile
    {
        public string GameId { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
        public Dictionary<string, string> VirtualRegistryKeys { get; set; } = new();
        public string SandboxPath { get; set; } = string.Empty;
        public string Priority { get; set; } = "Normal";
        public int LaunchTimeoutSeconds { get; set; } = 30;
    }
}
