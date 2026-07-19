using System;

namespace Sayra.Client.Launcher.Models
{
    public class LaunchOptions
    {
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
    }
}
