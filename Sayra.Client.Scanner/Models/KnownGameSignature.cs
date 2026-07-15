namespace Sayra.Client.Scanner.Models
{
    public class KnownGameSignature
    {
        public string ExecutableName { get; set; } = string.Empty;
        public string KnownPublisher { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Launcher { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string IconHint { get; set; } = string.Empty;
    }
}
