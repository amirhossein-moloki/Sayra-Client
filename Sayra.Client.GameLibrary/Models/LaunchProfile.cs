using System.Collections.Generic;

namespace Sayra.Client.GameLibrary.Models
{
    public class LaunchProfile
    {
        public string Arguments { get; set; } = string.Empty;
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();
        public string WorkingDirectory { get; set; } = string.Empty;
    }
}
