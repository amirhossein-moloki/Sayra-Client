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

        // Structured data model attributes mapping for high visual fidelity Game Launcher assets
        public string Title
        {
            get => Name;
            set => Name = value;
        }

        public string CoverImage
        {
            get => IconPath;
            set => IconPath = value;
        }

        public string LogoImage { get; set; } = string.Empty;
        public string BackgroundImage { get; set; } = string.Empty;
        public string Launcher { get; set; } = string.Empty;

        public string Genre
        {
            get => Category.Name;
            set
            {
                if (Category == null) Category = new GameCategory();
                Category.Name = value;
            }
        }

        public string Developer { get; set; } = string.Empty;
        public string ReleaseYear { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
