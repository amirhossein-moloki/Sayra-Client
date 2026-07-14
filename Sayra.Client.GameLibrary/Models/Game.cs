using System;

namespace Sayra.Client.GameLibrary.Models
{
    public class Game
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public GameCategory Category { get; set; } = new GameCategory();
        public bool Enabled { get; set; } = true;
        public GameSource Source { get; set; } = GameSource.Manual;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public LaunchProfile? LaunchProfile { get; set; }
    }
}
