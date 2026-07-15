namespace Sayra.Client.Scanner.Models
{
    public class DetectedApplication
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Launcher { get; set; } = string.Empty;
        public string ExecutableHash { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Game" or "Application"
        public int ConfidenceScore { get; set; }
    }
}
